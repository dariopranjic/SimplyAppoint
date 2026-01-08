using Microsoft.EntityFrameworkCore;
using SimplyAppoint.Models;

namespace SimplyAppoint.DataAccess.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {

        }

        public DbSet<Business> Businesses { get; set; }
    }
}
