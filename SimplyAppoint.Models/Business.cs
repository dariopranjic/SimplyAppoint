using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Threading;

namespace SimplyAppoint.Models
{
    [Index(nameof(Slug), IsUnique = true)]
    public class Business
    {
        [Key]
        public int Id { get; set; }
        [Required]
        [MaxLength(450)]
        public string OwnerUserId { get; set; }
        [Required]
        [MaxLength(200)]
        public string Name { get; set; }
        [Required]
        [MaxLength(120)]
        public string Slug { get; set; }
        [Required]
        [MaxLength(100)]
        public string TimeZoneId { get; set; }
        [Required]
        [MaxLength(50)]
        public string Phone { get; set; }
        [Required]
        [MaxLength(300)]
        public string Address { get; set; }
        [Required]
        public DateTimeOffset CreatedUtc { get; set; }

    }
}
