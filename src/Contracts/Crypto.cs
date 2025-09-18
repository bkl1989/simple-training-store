using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Keys.Cryptography;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Core.Tokens;

namespace Contracts
{
    // ----- Data you persist for the field -----
    public sealed class EncryptedField
    {
        public string CiphertextB64 { get; init; } = default!;
        public string NonceB64 { get; init; } = default!; // 12 bytes for GCM
        public string TagB64 { get; init; } = default!; // 16 bytes auth tag
        public string WrappedDekB64 { get; init; } = default!; // per-user wrapped key
        public string Alg { get; init; } = "AES-256-GCM";
        public string KeyRef { get; init; } = "user-dek-v1"; // optional key/version ref
    }

    //wrapper for local testing
    public class CryptographyService
    {
        private CryptographyClient _crypto = null;

        public CryptographyService(Uri keyId, TokenCredential? cred = null, KeyWrapAlgorithm? alg = null)
        {

        }

        public byte[] WrapKey (KeyWrapAlgorithm algorithm, byte[]dek)
        {
            return dek;
        }

        public byte[] UnwrapKey (KeyWrapAlgorithm algorithm, byte[]dek)
        {
            return dek;
        }
    }

    public class PiiCryptor
    {
        private readonly CryptographyService _crypto;
        private readonly KeyWrapAlgorithm _alg;
        public byte[] WrapKey(byte[] dek) =>
             _crypto.WrapKey(_alg, dek);

        public byte[] UnwrapKey(byte[] wrappedDek) =>
            _crypto.UnwrapKey(_alg, wrappedDek);
        public PiiCryptor (Uri keyId, TokenCredential? cred = null, KeyWrapAlgorithm? alg = null)
        {
            _crypto = new CryptographyService(keyId, cred ?? new DefaultAzureCredential());
            _alg = alg ?? KeyWrapAlgorithm.RsaOaep256;   // RSA-OAEP-256 is a solid default
                                                         // For Managed HSM symmetric keys, use KeyWrapAlgorithm.A256KW instead.
        }
        // Encrypt a user's field with their per-user DEK
        public EncryptedField EncryptTextField(Guid userId, string key, string value, byte[]? existingWrappedDek = null)
        {
            // 1) Get or create the per-user DEK (32 bytes for AES-256)
            byte[] dek;
            string wrappedDekB64;
            if (existingWrappedDek is null)
            {
                dek = RandomNumberGenerator.GetBytes(32);
                var wrapped = WrapKey(dek);
                wrappedDekB64 = Convert.ToBase64String(wrapped);
            }
            else
            {
                dek = UnwrapKey(Convert.FromBase64String(Encoding.UTF8.GetString(existingWrappedDek)));
                wrappedDekB64 = Encoding.UTF8.GetString(existingWrappedDek);
            }

            // 2) Encrypt with AES-GCM
            byte[] nonce = RandomNumberGenerator.GetBytes(12);             // unique per (DEK, encryption)
            byte[] plaintext = Encoding.UTF8.GetBytes(value);
            byte[] ciphertext = new byte[plaintext.Length];
            byte[] tag = new byte[16];

            // bind ciphertext to the user & field with AAD (prevents swapping)
            byte[] aad = Encoding.UTF8.GetBytes($"user:{userId}|field:{key}|v1");

            using (var aes = new AesGcm(dek))
                aes.Encrypt(nonce, plaintext, ciphertext, tag, aad);

            // hygiene: clear secrets from memory
            CryptographicOperations.ZeroMemory(dek);
            CryptographicOperations.ZeroMemory(plaintext);

            return new EncryptedField
            {
                CiphertextB64 = Convert.ToBase64String(ciphertext),
                NonceB64 = Convert.ToBase64String(nonce),
                TagB64 = Convert.ToBase64String(tag),
                WrappedDekB64 = wrappedDekB64,
                Alg = "AES-256-GCM",
                KeyRef = "user-dek-v1"
            };
        }

        // Decrypt it later
        public string DecryptTextField(Guid userId, string key, EncryptedField enc)
        {
            byte[] dek = UnwrapKey(Convert.FromBase64String(enc.WrappedDekB64));
            byte[] nonce = Convert.FromBase64String(enc.NonceB64);
            byte[] ciphertext = Convert.FromBase64String(enc.CiphertextB64);
            byte[] tag = Convert.FromBase64String(enc.TagB64);
            byte[] aad = Encoding.UTF8.GetBytes($"user:{userId}|field:{key}|v1");

            byte[] plaintext = new byte[ciphertext.Length];
            using (var aes = new AesGcm(dek))
                aes.Decrypt(nonce, ciphertext, tag, plaintext, aad);

            CryptographicOperations.ZeroMemory(dek);
            var value = Encoding.UTF8.GetString(plaintext);
            CryptographicOperations.ZeroMemory(plaintext);
            return value;
        }
    }

}
