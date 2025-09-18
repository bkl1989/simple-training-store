using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Reflection.Metadata;

namespace Learner
{
    public class LearnerUserDbContext(DbContextOptions<LearnerUserDbContext> options) : DbContext(options)
    {
        public DbSet<LearnerUser> LearnerUsers => Set<LearnerUser>();
    }
}
