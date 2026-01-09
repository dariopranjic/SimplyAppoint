using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Threading;

namespace SimplyAppoint.Models
{
    [Index(nameof(BusinessId))]
    [Index(nameof(BusinessId), nameof(Email), IsUnique = true)]
    public class BusinessCustomer
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int BusinessId { get; set; }

        [MaxLength(450)]
        public string? UserId { get; set; }

        [Required(AllowEmptyStrings = false)]
        [MaxLength(200)]
        public string FullName { get; set; } = default!;

        [Required(AllowEmptyStrings = false)]
        [MaxLength(200)]
        [EmailAddress]
        public string Email { get; set; } = default!;

        [MaxLength(50)]
        public string? Phone { get; set; }

        [Required]
        public bool IsActive { get; set; } = true;

        [Required]
        public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;

        public DateTimeOffset? UpdatedUtc { get; set; }
    }
}