using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace SimplyAppoint.Models.ViewModels.Services
{
    public class ServiceUpsertVM
    {
        public int? Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = "";

        [Required]
        [Range(1, 24 * 60)]
        public int DurationMinutes { get; set; }

        [Required]
        [Range(0.01, 1000000)]
        public decimal Price { get; set; }

        [Range(0, 24 * 60)]
        public int BufferBefore { get; set; }

        [Range(0, 24 * 60)]
        public int BufferAfter { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTimeOffset CreatedUtc { get; set; }

        public bool IsEdit => Id.HasValue;
    }
}