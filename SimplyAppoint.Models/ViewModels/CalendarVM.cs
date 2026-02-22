using SimplyAppoint.Models.Enums;
using System;
using System.Collections.Generic;

namespace SimplyAppoint.Models.ViewModels
{
    public class CalendarVM
    {
        public int TodayCount { get; set; }
        public int OpenSlotsCount { get; set; }
        public List<UpcomingAppt> UpcomingAppointments { get; set; } = new List<UpcomingAppt>();
        public string EventsJson { get; set; } = "[]";
        public class UpcomingAppt
        {
            public string Time { get; set; } = string.Empty;
            public string CustomerName { get; set; } = string.Empty;
            public string ServiceName { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
        }
    }
}