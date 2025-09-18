using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Order
{
    public sealed class OrderUser
    {
        public int Id { get; set; }
        [Required]
        public string EncryptedCreditCardNumber { get; set; } = string.Empty;
        [Required]
        public string EncryptedCreditCardName { get; set; } = string.Empty;
        [Required]
        public string EncryptedCreditCardExpiration { get; set; } = string.Empty;
    }
}
