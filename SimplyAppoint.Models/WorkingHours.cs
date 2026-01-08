using Microsoft.EntityFrameworkCore;
using SimplyAppoint.Models.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace SimplyAppoint.Models
{
    [Index(nameof(BusinessId))]
    [Index(nameof(BusinessId), nameof(Weekday), IsUnique = true)]
    public class WorkingHours
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int BusinessId { get; set; }

        [Required]
        [Range(1, 7)]
        public Weekday Weekday { get; set; }

        [Required]
        public bool IsClosed { get; set; } = false;

        public TimeOnly? OpenTime { get; set; }
        public TimeOnly? CloseTime { get; set; }
    }
}
