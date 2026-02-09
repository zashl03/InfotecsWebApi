using InfotecsWebApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Collections.Generic;

namespace InfotecsWebApi.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
        {
            Database.EnsureCreated();
        }

        public DbSet<ValueEntry> Values { get; set; }
        public DbSet<ResultEntry> Results { get; set; }
    }
}
