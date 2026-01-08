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
        public DbSet<Service> Services { get; set; }
        public DbSet<BookingPolicy> BookingPolicies { get; set; }
        public DbSet<WorkingHours> WorkingHours { get; set; }
        public DbSet<TimeOff> TimeOffs { get; set; }
        public DbSet<Appointment> Appointments { get; set; }

    }
}
