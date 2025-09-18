using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Auth
{
    public sealed class AuthUser
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public string EmailAddress { get; set; }
        [Required]
        public byte [] HashedPassword { get; set; } = [];
        [Required]
        public byte[] Salt { get; set; } = [];
        [Required]
        public Guid AggregateId { get; set; }
    }

    public class MyDbContext : DbContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            //Since we can guarantee the size of the hashed password, ensure it's stored that way in the DB
            modelBuilder.Entity<AuthUser>()
                .Property(u => u.HashedPassword)
                .HasColumnType("binary(32)")
                .IsFixedLength()
                .IsRequired();
            //Since we can guarantee the size of the salt, ensure it's stored that way in the DB
            modelBuilder.Entity<AuthUser>()
                .Property(u => u.Salt)
                .HasColumnType("binary(16)")
                .IsFixedLength()
                .IsRequired();
        }
    }
}
