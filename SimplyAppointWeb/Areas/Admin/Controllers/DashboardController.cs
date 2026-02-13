using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SimplyAppoint.DataAccess.Repository.IRepository;
using SimplyAppoint.Models;
using SimplyAppoint.Models.Enums;
using SimplyAppoint.Models.ViewModels;
using System.Diagnostics;

namespace SimplyAppointWeb.Controllers
{
    [Area("Admin")]
    public class DashboardController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserManager<IdentityUser> _userManager;

        public DashboardController(IUnitOfWork unitOfWork, UserManager<IdentityUser> userManager)
        {
            _unitOfWork = unitOfWork;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var now = DateTimeOffset.UtcNow;
            var todayDate = now.Date;
            var sevenDaysAgo = now.AddDays(-7);

            var appointments = _unitOfWork.Appointment.GetAll(includeProperties: "Service").ToList();

            // FIXED: Removed the EF.Property check that caused the Exception in your image.
            // This now gets the total count safely.
            var totalUsersCount = await _userManager.Users.CountAsync();

            var model = new DashboardVM
            {
                TodayAppointmentsCount = appointments.Count(a => a.StartUtc.Date == todayDate),
                UpcomingSevenDaysCount = appointments.Count(a => a.StartUtc.Date >= todayDate && a.StartUtc.Date <= todayDate.AddDays(7)),
                CancellationsSevenDays = appointments.Count(a => a.Status == AppointmentStatus.Cancelled && a.CancelledUtc >= sevenDaysAgo),
                NewCustomersThirtyDays = totalUsersCount, // Safe count

                NextAppointments = appointments
                    .Where(a => a.StartUtc >= now && a.Status != AppointmentStatus.Cancelled)
                    .OrderBy(a => a.StartUtc)
                    .Take(5)
                    .Select(a => new AppointmentSummaryViewModel
                    {
                        Id = a.Id,
                        Time = a.StartUtc.ToLocalTime().ToString("HH:mm"),
                        CustomerName = a.CustomerName ?? "Unknown",
                        ServiceName = a.Service?.Name ?? "General",
                        Status = a.Status
                    }).ToList()
            };

            // Populate chart data
            for (int i = 6; i >= 0; i--)
            {
                var day = todayDate.AddDays(-i);
                model.ChartLabels7Days.Add(day.ToString("ddd"));
                model.ChartValues7Days.Add(appointments.Count(a => a.CreatedUtc.Date == day.Date));
            }

            for (int i = 29; i >= 0; i--)
            {
                var day = todayDate.AddDays(-i);
                model.ChartLabels30Days.Add(day.ToString("MM/dd"));
                model.ChartValues30Days.Add(appointments.Count(a => a.CreatedUtc.Date == day.Date));
            }

            // Occupancy logic
            var todayAppts = appointments.Where(a => a.StartUtc.Date == todayDate && a.Status != AppointmentStatus.Cancelled).ToList();
            model.MorningOccupancy = CalculateOccupancy(todayAppts, 9, 12);
            model.AfternoonOccupancy = CalculateOccupancy(todayAppts, 12, 15);
            model.EveningOccupancy = CalculateOccupancy(todayAppts, 15, 18);

            return View(model);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        public IActionResult Support()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        private int CalculateOccupancy(List<Appointment> appts, int startHour, int endHour)
        {
            int totalMinutesInBlock = (endHour - startHour) * 60;

            // Filters appointments that occur within the local time block
            int bookedMinutes = appts
                .Where(a => a.StartUtc.ToLocalTime().Hour >= startHour && a.StartUtc.ToLocalTime().Hour < endHour)
                .Sum(a => a.Service != null ? a.Service.DurationMinutes : 30);

            if (totalMinutesInBlock == 0) return 0;

            double ratio = (double)bookedMinutes / totalMinutesInBlock;
            return (int)Math.Min(Math.Round(ratio * 100), 100);
        }
    }
}