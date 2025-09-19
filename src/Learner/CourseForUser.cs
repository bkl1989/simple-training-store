using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Learner
{
    public sealed class CourseForUser
    {
        public int Id { get; set; }
        [Required]
        public string Title { get; set; } = string.Empty;
        [Required]
        public string Description { get; set; } = string.Empty;
        [Required]
        public Guid CourseAggregateId { get; set; }
        [Required]
        public Guid UserAggregateId { get; set; }
    }
}
