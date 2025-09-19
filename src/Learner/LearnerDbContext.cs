using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Reflection.Metadata;

namespace Learner
{
    public class LearnerDbContext(DbContextOptions<LearnerDbContext> options) : DbContext(options)
    {
        public DbSet<LearnerUser> LearnerUsers => Set<LearnerUser>();
        public DbSet<LearnerCourse> LearnerCourses => Set<LearnerCourse>();
    }
}
