using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using SimplyAppoint.DataAccess.Data;
using SimplyAppoint.Models;
using SimplyAppoint.Models.ViewModels;
using System.Diagnostics;

namespace SimplyAppointWeb.Controllers
{
    [Area("Admin")]
    public class CalendarController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CalendarController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var todayUtc = DateTimeOffset.UtcNow.Date;

            var appointments = await _context.Appointments
                .Include(a => a.Service)
                .Where(a => a.CancelledUtc == null)
                .ToListAsync();

            var viewModel = new CalendarVM
            {
                TodayCount = appointments.Count(a => a.StartUtc.ToLocalTime().Date == DateTime.Today),

                OpenSlotsCount = 5,

                UpcomingAppointments = appointments
                    .Where(a => a.StartUtc >= DateTimeOffset.UtcNow)
                    .OrderBy(a => a.StartUtc)
                    .Take(3)
                    .Select(a => new CalendarVM.UpcomingAppt
                    {
                        Time = a.StartUtc.ToLocalTime().ToString("HH:mm"),
                        CustomerName = a.CustomerName,
                        ServiceName = a.Service.Name,
                        Status = a.Status.ToString()
                    }).ToList()
            };

            var events = appointments.Select(a => new
            {
                id = a.Id,
                title = a.CustomerName,
                start = a.StartUtc.ToString("yyyy-MM-ddTHH:mm:ss"),
                end = a.EndUtc.ToString("yyyy-MM-ddTHH:mm:ss"),
                backgroundColor = GetStatusColor(a.Status),
                borderColor = GetStatusColor(a.Status),
                customerName = a.CustomerName,
                serviceName = a.Service.Name,
                status = a.Status.ToString(),
                phone = a.CustomerPhone ?? "N/A",
                price = a.Price.ToString("C"),
                notes = a.Notes ?? "No notes."
            });

            viewModel.EventsJson = JsonConvert.SerializeObject(events);

            return View(viewModel);
        }
        private string GetStatusColor(SimplyAppoint.Models.Enums.AppointmentStatus status)
        {
            return status switch
            {
                SimplyAppoint.Models.Enums.AppointmentStatus.Confirmed => "#10b981", 
                SimplyAppoint.Models.Enums.AppointmentStatus.Pending => "#f59e0b",   
                _ => "#3b7ddd" 
            };
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}