using Microsoft.EntityFrameworkCore;
using SecureLoginApi.Pg.Models;

namespace SecureLoginApi.Pg.Data.Context
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }
        public DbSet<LoginApprovalRequest> LoginApprovalRequests { get; set; }
        public DbSet<User> Users { get; set; }
    }
}
