using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace SimplyAppoint.Models
{
    [Index(nameof(BusinessId))]
    [Index(nameof(BusinessId), nameof(Name), IsUnique = true)]
    public class Service
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int BusinessId { get; set; }

        [Required(AllowEmptyStrings = false)]
        [MaxLength(200)]
        public string Name { get; set; } = default!;

        [Required]
        [Range(1, 24 * 60)]
        public int DurationMinutes { get; set; }

        [Required]
        [Precision(10, 2)]
        [Range(0, 1000000)]
        public decimal Price { get; set; }

        [Range(0, 24 * 60)]
        public int BufferBefore { get; set; }

        [Range(0, 24 * 60)]
        public int BufferAfter { get; set; }

        [Required]
        public bool IsActive { get; set; } = true;

        [Required]
        public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    }
}