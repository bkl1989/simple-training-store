using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Reflection.Metadata;

namespace Order
{
    public class OrderDbContext(DbContextOptions<OrderDbContext> options) : DbContext(options)
    {
        public DbSet<OrderUser> OrderUsers => Set<OrderUser>();
        public DbSet<OrderCourse> OrderCourses => Set<OrderCourse>();
    }
}
