using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Reflection.Metadata;

namespace Order
{
    public class OrderUserDbContext(DbContextOptions<OrderUserDbContext> options) : DbContext(options)
    {
        public DbSet<OrderUser> OrderUsers => Set<OrderUser>();
    }
}
