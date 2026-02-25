using Microsoft.AspNetCore.Mvc.Rendering;
using SimplyAppoint.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace SimplyAppoint.Models.ViewModels
{
    public class AppointmentsVM : IValidatableObject
    {
        public int? Id { get; set; }

        [Required]
        public int ServiceId { get; set; }

        [MaxLength(200)]
        public string? CustomerName { get; set; }

        [EmailAddress, MaxLength(200)]
        public string? CustomerEmail { get; set; }

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
        public string? ServiceLabel { get; set; }

        // Available Times
        public List<string> AvailableTimes { get; set; } = new();
        public string? AvailabilityMessage { get; set; }
        public int MaxSlotsToShow { get; set; } = 9;

        // Booking policy
        public int SlotIntervalMinutes { get; set; } = 30;
        public int AdvanceNoticeMinutes { get; set; } = 0;
        public int CancellationWindowMinutes { get; set; } = 0;
        public int MaxAdvanceDays { get; set; } = 30;

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            // Name OR Email must be provided
            if (string.IsNullOrWhiteSpace(CustomerName) && string.IsNullOrWhiteSpace(CustomerEmail))
            {
                yield return new ValidationResult(
                    "Please provide either customer name or customer email.",
                    new[] { nameof(CustomerName), nameof(CustomerEmail) }
                );
            }
        }
    }
}