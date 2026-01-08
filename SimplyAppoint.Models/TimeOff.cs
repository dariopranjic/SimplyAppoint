using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace SimplyAppoint.Models
{
    [Index(nameof(BusinessId))]
    [Index(nameof(BusinessId), nameof(StartUtc))]
    public class TimeOff
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int BusinessId { get; set; }

        [Required]
        public DateTimeOffset StartUtc { get; set; }

        [Required]
        public DateTimeOffset EndUtc { get; set; }

        [MaxLength(200)]
        public string? Reason { get; set; }

        [Required]
        public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    }
}