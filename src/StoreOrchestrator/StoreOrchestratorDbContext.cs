using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StoreOrchestrator
{
    using Microsoft.EntityFrameworkCore;
    using System.Reflection.Metadata;

    public class StoreOrchestratorDbContext(DbContextOptions<StoreOrchestratorDbContext> options) : DbContext(options)
    {
        public DbSet<StoreOrchestratorUser> StoreOrchestratorUsers => Set<StoreOrchestratorUser>();
        public DbSet<CreateUserSaga> CreateUserSagas => Set<CreateUserSaga>();
        public DbSet<CreateCourseSaga> CreateCourseSagas => Set<CreateCourseSaga>();
    }
}
