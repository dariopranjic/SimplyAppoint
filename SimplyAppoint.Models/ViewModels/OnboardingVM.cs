using System;
using System.Collections.Generic;
using System.Text;

namespace SimplyAppoint.Models.ViewModels
{
    public class OnboardingVM
    {
        public Business Business { get; set; } = new();
        public List<Service> Services { get; set; } = new();
        public List<WorkingHours> WorkingHours { get; set; } = new();
        public BookingPolicy BookingPolicy { get; set; } = new();
    }
}
