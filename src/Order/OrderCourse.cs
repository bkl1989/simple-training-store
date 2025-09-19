using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Order
{
    public sealed class OrderCourse
    {
        public int Id { get; set; }
        [Required]

        public Guid AggregateId { get; set; }
        public string Title { get; set; }
        public int Price { get; set; }
    }
}
