using SimplyAppoint.Models.Enums;
using System;
using System.Collections.Generic;

namespace SimplyAppoint.Models.ViewModels
{
    public class DashboardVM
    {
        // Stat Cards
        public int TodayAppointmentsCount { get; set; }
        public int TodayChange { get; set; }
        public int UpcomingSevenDaysCount { get; set; }
        public double UpcomingGrowthPercentage { get; set; }
        public int CancellationsSevenDays { get; set; }
        public int NewCustomersThirtyDays { get; set; }

        // Table Data
        public List<AppointmentSummaryViewModel> NextAppointments { get; set; } = new();

        // Today's Schedule Progress Bars (0 to 100)
        public int MorningOccupancy { get; set; }   // 09:00 - 12:00
        public int AfternoonOccupancy { get; set; } // 12:00 - 15:00
        public int EveningOccupancy { get; set; }   // 15:00 - 18:00

        // Chart Data - Divided for the Dropdown Toggle
        public List<string> ChartLabels7Days { get; set; } = new();
        public List<int> ChartValues7Days { get; set; } = new();

        public List<string> ChartLabels30Days { get; set; } = new();
        public List<int> ChartValues30Days { get; set; } = new();
    }

    public class AppointmentSummaryViewModel
    {
        public int Id { get; set; }
        public string Time { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string ServiceName { get; set; } = string.Empty;
        public AppointmentStatus Status { get; set; }
    }
}