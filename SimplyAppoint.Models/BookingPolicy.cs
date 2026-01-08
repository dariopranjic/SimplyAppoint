using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace SimplyAppoint.Models
{
    public class BookingPolicy
    {
        [Key]
        public int BusinessId { get; set; }

        [Required]
        [Range(5, 24 * 60)]
        public int SlotIntervalMinutes { get; set; }

        [Required]
        [Range(0, 24 * 60 * 7)]
        public int AdvanceNoticeMinutes { get; set; }

        [Required]
        [Range(0, 24 * 60 * 7)]
        public int CancellationWindowMinutes { get; set; }

        [Required]
        [Range(0, 365)]
        public int MaxAdvanceDays { get; set; }
    }
}
