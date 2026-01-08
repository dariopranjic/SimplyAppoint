using Microsoft.EntityFrameworkCore;
using SimplyAppoint.Models.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace SimplyAppoint.Models
{
    [Index(nameof(ServiceId), nameof(StartUtc))]
    [Index(nameof(BusinessId), nameof(StartUtc))]
    public class Appointment
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int BusinessId { get; set; }

        [Required]
        public int ServiceId { get; set; }

        [Required(AllowEmptyStrings = false)]
        [MaxLength(200)]
        public string CustomerName { get; set; } = default!;

        [Required(AllowEmptyStrings = false)]
        [MaxLength(200)]
        [EmailAddress]
        public string CustomerEmail { get; set; } = default!;

        [MaxLength(50)]
        public string? CustomerPhone { get; set; }

        [Required]
        public DateTimeOffset StartUtc { get; set; }

        [Required]
        public DateTimeOffset EndUtc { get; set; }

        [Required]
        public AppointmentStatus Status { get; set; } = AppointmentStatus.Pending;

        [Required]
        [Range(1, 24 * 60)]
        public int DurationMinutes { get; set; }

        [Required]
        [Precision(10, 2)]
        [Range(0, 1000000)]
        public decimal Price { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        public DateTimeOffset? CancelledUtc { get; set; }

        [MaxLength(200)]
        public string? CancellationReason { get; set; }

        [Required]
        public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    }
}