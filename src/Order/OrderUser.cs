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

        public int UserAggregateId { get; set; }
    }
}
