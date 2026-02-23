using Microsoft.AspNetCore.Mvc.Rendering;
using SimplyAppoint.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace SimplyAppoint.Models.ViewModels
{
    public class AppointmentsIndexVM
    {
        // Filters (optional)
        public string? Query { get; set; }
        public string DateRange { get; set; } = "week";   // today | week | month | all
        public string Status { get; set; } = "all";       // all | Confirmed | Pending | Cancelled

        // Stats
        public int TodayCount { get; set; }
        public int Next7DaysCount { get; set; }
        public decimal RevenueWeek { get; set; }
        public int CancellationsWeek { get; set; }

        public int TotalShown { get; set; }

        // Table
        public List<AppointmentRowVM> Rows { get; set; } = new();

        public int OpenSlotsCount { get; set; }
        public List<UpcomingApptVM> Upcoming { get; set; } = new();

        public class AppointmentRowVM
        {
            public int Id { get; set; }
            public string WhenTime { get; set; } = "";
            public string WhenDate { get; set; } = "";
            public string CustomerDisplay { get; set; } = "";
            public string CustomerSub { get; set; } = "";
            public string ServiceName { get; set; } = "";
            public AppointmentStatus Status { get; set; }
        }

        public class UpcomingApptVM
        {
            public int Id { get; set; }
            public string Time { get; set; } = "";
            public string Customer { get; set; } = "";
            public string Service { get; set; } = "";
            public AppointmentStatus Status { get; set; }
        }
    }

    public class AppointmentsVM
    {
        public int? Id { get; set; }

        [Required]
        public int ServiceId { get; set; }

        [Required, MaxLength(200)]
        public string CustomerName { get; set; } = "";

        [Required, EmailAddress, MaxLength(200)]
        public string CustomerEmail { get; set; } = "";

        [MaxLength(50)]
        public string? CustomerPhone { get; set; }

        [Required]
        public AppointmentStatus Status { get; set; } = AppointmentStatus.Confirmed;

        [Required]
        public string Date { get; set; } = "";       // yyyy-MM-dd (business local date)

        [Required]
        public string StartTime { get; set; } = "";  // HH:mm

        [Range(1, 24 * 60)]
        public int? DurationMinutes { get; set; }

        [Range(0, 1000000)]
        public decimal? PriceOverride { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        // Dropdowns
        public List<SelectListItem> ServiceOptions { get; set; } = new();
        public string? ServiceLabel { get; set; } // for Edit header display
    }
}