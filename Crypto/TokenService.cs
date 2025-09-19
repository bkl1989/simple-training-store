using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration.UserSecrets;
using Microsoft.Extensions.Configuration;

namespace Auth
{
    public interface ITokenService
    {
        string BuildToken(string key, string issuer, IEnumerable<string> audience, string userName);
    }
    public class TokenService : ITokenService
    {
        private TimeSpan ExpiryDuration = new TimeSpan(0, 30, 0);

        public static string getJWTKey()
        {
            //if this is set in user secrets, use that value
            var config = new ConfigurationBuilder()
            .AddUserSecrets<TokenService>() // Specify the assembly containing the UserSecretsId
            .Build();

            return config["JWTKey"]!;
        }
        public string BuildToken(string key, string issuer, IEnumerable<string> audience, string userName)
        {
            var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.UniqueName, userName),
        };
            claims.AddRange(audience.Select(aud => new Claim(JwtRegisteredClaimNames.Aud, aud)));

            //TODO: should this be an asymmetric signature? maybe I'm missing something
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256Signature);
            var tokenDescriptor = new JwtSecurityToken(issuer, issuer, claims,
                expires: DateTime.Now.Add(ExpiryDuration), signingCredentials: credentials);
            return new JwtSecurityTokenHandler().WriteToken(tokenDescriptor);
        }
    }

}
