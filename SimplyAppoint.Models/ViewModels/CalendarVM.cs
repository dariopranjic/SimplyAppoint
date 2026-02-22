using System;
using System.Collections.Generic;

namespace SimplyAppoint.Models.ViewModels
{
    public class CalendarVM
    {
        public int TodayCount { get; set; }
        public int OpenSlotsCount { get; set; }
        public List<UpcomingAppt> UpcomingAppointments { get; set; } = new();
        public string EventsJson { get; set; } = "[]";
        public string SlotMinTime { get; set; } = "07:00:00";
        public string SlotMaxTime { get; set; } = "20:00:00";
        public string BusinessHoursJson { get; set; } = "[]"; 
        public string HiddenDaysJson { get; set; } = "[]";
        public string SlotDuration { get; set; } = "00:30:00";   
        public string SnapDuration { get; set; } = "00:30:00";   
        public string BookableStartDateIso { get; set; } = "";   
        public string BookableEndDateIso { get; set; } = "";    
        public string MinBookableStartIso { get; set; } = "";   
        public bool ShowTimeOff { get; set; } = true;

        public class UpcomingAppt
        {
            public string Time { get; set; } = string.Empty;
            public string CustomerName { get; set; } = string.Empty;
            public string ServiceName { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public int? AppointmentId { get; set; }
        }
    }
}