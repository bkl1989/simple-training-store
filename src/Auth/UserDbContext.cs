using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Auth
{
    using Microsoft.EntityFrameworkCore;
    using System.Reflection.Metadata;

    public class UserDBContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<User> Users => Set<User>();
    }
}
