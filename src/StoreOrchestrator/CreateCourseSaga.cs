using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StoreOrchestrator
{
    public sealed class CreateCourseSaga
    {
        public int Id { get; set; }
        [Required]
        public Guid correlationId { get; set; }
        [Required]
        public Guid aggregateId { get; set; }
    }
}
