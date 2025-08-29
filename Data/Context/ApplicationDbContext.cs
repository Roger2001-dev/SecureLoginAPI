using Microsoft.EntityFrameworkCore;
using SecureLoginApi.Pg.Models;

namespace SecureLoginApi.Pg.Data.Context
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
    }
}
