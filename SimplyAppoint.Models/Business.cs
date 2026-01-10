using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Threading;

namespace SimplyAppoint.Models
{
    [Index(nameof(Slug), IsUnique = true)]
    [Index(nameof(OwnerUserId))]
    public class Business
    {
        [Key]
        public int Id { get; set; }

        [Required(AllowEmptyStrings = false)]
        [MaxLength(450)]
        public string OwnerUserId { get; set; } = default!;

        [Required(AllowEmptyStrings = false)]
        [MaxLength(200)]
        public string Name { get; set; } = default!;

        [Required(AllowEmptyStrings = false)]
        [MaxLength(120)]
        public string Slug { get; set; } = default!;

        [Required(AllowEmptyStrings = false)]
        [MaxLength(100)]
        public string TimeZoneId { get; set; } = default!;

        [Required(AllowEmptyStrings = false)]
        [MaxLength(50)]
        public string Phone { get; set; } = default!;

        [Required(AllowEmptyStrings = false)]
        [MaxLength(300)]
        public string Address { get; set; } = default!;

        [Required]
        public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
        public bool IsOnboardingComplete { get; set; } = false;
    }
}