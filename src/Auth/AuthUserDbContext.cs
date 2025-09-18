using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Auth
{
    using Microsoft.EntityFrameworkCore;
    using System.Reflection.Metadata;

    public class AuthUserDbContext(DbContextOptions<AuthUserDbContext> options) : DbContext(options)
    {
        public DbSet<AuthUser> AuthUsers => Set<AuthUser>();
    }
}
