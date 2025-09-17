using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Auth
{
    public sealed class User
    {
        public int Id { get; set; }
        [Required]
        public string EmailAddress { get; set; } = string.Empty;
        [Required]
        public byte [] HashedPassword { get; set; } = [];
    }

    public class MyDbContext : DbContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            //Since we can guarantee the size of the hashed password, ensure it's stored that way in the DB
            modelBuilder.Entity<User>()
                .Property(u => u.HashedPassword)
                .HasColumnType("binary(32)")
                .IsFixedLength()
                .IsRequired();
        }
    }
}
