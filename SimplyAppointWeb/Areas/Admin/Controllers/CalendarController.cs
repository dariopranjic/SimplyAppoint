using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using SimplyAppoint.DataAccess.Repository.IRepository;
using SimplyAppoint.Models;
using SimplyAppoint.Models.Enums;
using SimplyAppoint.Models.ViewModels;
using System.Diagnostics;

namespace SimplyAppointWeb.Controllers
{
    [Area("Admin")]
    [Authorize]
    public class CalendarController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserManager<IdentityUser> _userManager;

        public CalendarController(IUnitOfWork unitOfWork, UserManager<IdentityUser> userManager)
        {
            _unitOfWork = unitOfWork;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            // 1) Current user + their business
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var business = _unitOfWork.Business.Get(b => b.OwnerUserId == user.Id);
            if (business == null)
            {
                return View(BuildEmptyVmWithFallbacks());
            }

            var businessId = business.Id;

            // 2) Business timezone (StartUtc/EndUtc are UTC)
            TimeZoneInfo tz;
            try
            {
                tz = TimeZoneInfo.FindSystemTimeZoneById(business.TimeZoneId);
            }
            catch
            {
                tz = TimeZoneInfo.Local; // fallback if TimeZoneId is invalid on server
            }

            var nowUtc = DateTimeOffset.UtcNow;
            var nowBiz = TimeZoneInfo.ConvertTime(nowUtc, tz);
            var todayBizDate = nowBiz.Date;

            // 3) Load data through UnitOfWork
            var policy = _unitOfWork.BookingPolicy.Get(p => p.BusinessId == businessId);

            var workingHours = _unitOfWork.WorkingHours
                .GetAll(w => w.BusinessId == businessId)
                .ToList();

            var appointments = _unitOfWork.Appointment
                .GetAll(
                    a => a.BusinessId == businessId
                      && a.CancelledUtc == null
                      && a.Status != AppointmentStatus.Cancelled,
                    includeProperties: "Service"
                )
                .ToList();

            // TimeOff: returns empty list if none exist
            var timeOff = _unitOfWork.TimeOff
                .GetAll(t => t.BusinessId == businessId)
                .ToList();

            // 4) Build VM
            var vm = new CalendarVM();

            // BookingPolicy -> slot durations + booking window
            var slotInterval = policy?.SlotIntervalMinutes ?? 30;
            vm.SlotDuration = ToDuration(slotInterval);
            vm.SnapDuration = ToDuration(slotInterval);

            vm.BookableStartDateIso = todayBizDate.ToString("yyyy-MM-dd");
            vm.BookableEndDateIso = todayBizDate.AddDays((policy?.MaxAdvanceDays ?? 30) + 1).ToString("yyyy-MM-dd");

            var minBookableBiz = nowBiz.AddMinutes(policy?.AdvanceNoticeMinutes ?? 0);
            vm.MinBookableStartIso = minBookableBiz.ToString("yyyy-MM-ddTHH:mm:ss");

            // WorkingHours -> slotMin/Max + businessHours + hiddenDays
            PopulateWorkingHoursConfig(vm, workingHours);

            // Widgets
            vm.TodayCount = appointments.Count(a =>
            {
                var startBiz = TimeZoneInfo.ConvertTime(a.StartUtc, tz);
                return startBiz.Date == todayBizDate;
            });

            vm.UpcomingAppointments = appointments
                .Where(a => a.StartUtc >= nowUtc)
                .OrderBy(a => a.StartUtc)
                .Take(3)
                .Select(a => new CalendarVM.UpcomingAppt
                {
                    Time = TimeZoneInfo.ConvertTime(a.StartUtc, tz).ToString("HH:mm"),
                    CustomerName = a.CustomerName,
                    ServiceName = a.Service?.Name ?? "Service",
                    Status = a.Status.ToString()
                })
                .ToList();

            // Open slots (TODAY), respects advance notice + buffers + time off
            vm.OpenSlotsCount = CalculateOpenSlotsCountForToday(
                tz: tz,
                todayBizDate: todayBizDate,
                workingHours: workingHours,
                slotIntervalMinutes: slotInterval,
                minStartBiz: minBookableBiz.DateTime,
                appointments: appointments,
                timeOff: timeOff
            );

            // 5) Events JSON for FullCalendar (appointments + optional time off background)
            var events = new List<object>();

            foreach (var a in appointments)
            {
                var startBiz = TimeZoneInfo.ConvertTime(a.StartUtc, tz);
                var endBiz = TimeZoneInfo.ConvertTime(a.EndUtc, tz);

                events.Add(new
                {
                    id = a.Id,
                    start = startBiz.ToString("yyyy-MM-ddTHH:mm:ss"),
                    end = endBiz.ToString("yyyy-MM-ddTHH:mm:ss"),
                    backgroundColor = GetStatusColor(a.Status),
                    borderColor = GetStatusColor(a.Status),

                    // extendedProps (modal)
                    customerName = a.CustomerName,
                    serviceName = a.Service?.Name ?? "Service",
                    status = a.Status.ToString(),
                    phone = a.CustomerPhone ?? "N/A",
                    price = a.Price.ToString("0.00"),
                    notes = string.IsNullOrWhiteSpace(a.Notes) ? "No notes." : a.Notes
                });
            }

            if (vm.ShowTimeOff && timeOff.Count > 0)
            {
                foreach (var t in timeOff)
                {
                    var startBiz = TimeZoneInfo.ConvertTime(t.StartUtc, tz);
                    var endBiz = TimeZoneInfo.ConvertTime(t.EndUtc, tz);

                    events.Add(new
                    {
                        id = $"timeoff-{t.Id}",
                        start = startBiz.ToString("yyyy-MM-ddTHH:mm:ss"),
                        end = endBiz.ToString("yyyy-MM-ddTHH:mm:ss"),
                        display = "background",
                        backgroundColor = "rgba(239, 68, 68, .18)",
                        borderColor = "rgba(239, 68, 68, .28)",
                        reason = t.Reason ?? "Time off"
                    });
                }
            }

            vm.EventsJson = JsonConvert.SerializeObject(events);

            return View(vm);
        }

        // ---------------- Helpers ----------------

        private CalendarVM BuildEmptyVmWithFallbacks()
        {
            return new CalendarVM
            {
                TodayCount = 0,
                OpenSlotsCount = 0,
                UpcomingAppointments = new List<CalendarVM.UpcomingAppt>(),
                EventsJson = "[]",

                SlotMinTime = "07:00:00",
                SlotMaxTime = "20:00:00",
                SlotDuration = "00:30:00",
                SnapDuration = "00:30:00",
                BusinessHoursJson = "[]",
                HiddenDaysJson = "[]",

                BookableStartDateIso = DateTime.Today.ToString("yyyy-MM-dd"),
                BookableEndDateIso = DateTime.Today.AddDays(31).ToString("yyyy-MM-dd"),
                MinBookableStartIso = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss")
            };
        }

        private void PopulateWorkingHoursConfig(CalendarVM vm, List<WorkingHours> workingHours)
        {
            if (workingHours == null || workingHours.Count == 0)
            {
                vm.SlotMinTime = "07:00:00";
                vm.SlotMaxTime = "20:00:00";
                vm.BusinessHoursJson = "[]";
                vm.HiddenDaysJson = "[]";
                return;
            }

            var openDays = workingHours
                .Where(w => !w.IsClosed && w.OpenTime.HasValue && w.CloseTime.HasValue)
                .ToList();

            if (openDays.Count == 0)
            {
                vm.SlotMinTime = "07:00:00";
                vm.SlotMaxTime = "20:00:00";
                vm.BusinessHoursJson = "[]";
                vm.HiddenDaysJson = "[]";
                return;
            }

            var minOpen = openDays.Min(x => x.OpenTime!.Value);
            var maxClose = openDays.Max(x => x.CloseTime!.Value);

            vm.SlotMinTime = minOpen.ToString("HH:mm:ss");
            vm.SlotMaxTime = maxClose.ToString("HH:mm:ss");

            // FullCalendar: 0=Sunday ... 6=Saturday
            // Assumption: Weekday enum has Monday..Sunday members
            int ToFcDay(Weekday d) => d switch
            {
                Weekday.Monday => 1,
                Weekday.Tuesday => 2,
                Weekday.Wednesday => 3,
                Weekday.Thursday => 4,
                Weekday.Friday => 5,
                Weekday.Saturday => 6,
                Weekday.Sunday => 0,
                _ => 1
            };

            var businessHours = openDays.Select(w => new
            {
                daysOfWeek = new[] { ToFcDay(w.Weekday) },
                startTime = w.OpenTime!.Value.ToString("HH:mm:ss"),
                endTime = w.CloseTime!.Value.ToString("HH:mm:ss")
            }).ToList();

            var hiddenDays = workingHours
                .Where(w => w.IsClosed)
                .Select(w => ToFcDay(w.Weekday))
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            vm.BusinessHoursJson = JsonConvert.SerializeObject(businessHours);
            vm.HiddenDaysJson = JsonConvert.SerializeObject(hiddenDays);
        }

        private int CalculateOpenSlotsCountForToday(
            TimeZoneInfo tz,
            DateTime todayBizDate,
            List<WorkingHours> workingHours,
            int slotIntervalMinutes,
            DateTime minStartBiz,
            List<Appointment> appointments,
            List<TimeOff> timeOff)
        {
            // Map DayOfWeek -> Weekday
            Weekday? todayEnum = todayBizDate.DayOfWeek switch
            {
                DayOfWeek.Monday => Weekday.Monday,
                DayOfWeek.Tuesday => Weekday.Tuesday,
                DayOfWeek.Wednesday => Weekday.Wednesday,
                DayOfWeek.Thursday => Weekday.Thursday,
                DayOfWeek.Friday => Weekday.Friday,
                DayOfWeek.Saturday => Weekday.Saturday,
                DayOfWeek.Sunday => Weekday.Sunday,
                _ => null
            };
            if (todayEnum == null) return 0;

            var wh = workingHours.FirstOrDefault(w => w.Weekday == todayEnum.Value);
            if (wh == null || wh.IsClosed || !wh.OpenTime.HasValue || !wh.CloseTime.HasValue) return 0;

            var open = todayBizDate.Add(wh.OpenTime.Value.ToTimeSpan());
            var close = todayBizDate.Add(wh.CloseTime.Value.ToTimeSpan());
            if (close <= open) return 0;

            var blocked = new List<(DateTime start, DateTime end)>();

            // TimeOff blocks (business tz)
            foreach (var t in timeOff)
            {
                var s = TimeZoneInfo.ConvertTime(t.StartUtc, tz).DateTime;
                var e = TimeZoneInfo.ConvertTime(t.EndUtc, tz).DateTime;
                if (e <= open || s >= close) continue;
                blocked.Add((Max(s, open), Min(e, close)));
            }

            // Appointment blocks with service buffers (business tz)
            foreach (var a in appointments)
            {
                var s0 = TimeZoneInfo.ConvertTime(a.StartUtc, tz).DateTime;
                var e0 = TimeZoneInfo.ConvertTime(a.EndUtc, tz).DateTime;

                var before = a.Service?.BufferBefore ?? 0;
                var after = a.Service?.BufferAfter ?? 0;

                var s = s0.AddMinutes(-before);
                var e = e0.AddMinutes(after);

                if (e <= open || s >= close) continue;
                blocked.Add((Max(s, open), Min(e, close)));
            }

            // Merge overlaps
            blocked = blocked.Where(b => b.end > b.start).OrderBy(b => b.start).ToList();
            var merged = new List<(DateTime start, DateTime end)>();
            foreach (var b in blocked)
            {
                if (merged.Count == 0) { merged.Add(b); continue; }
                var last = merged[^1];
                if (b.start <= last.end)
                    merged[^1] = (last.start, b.end > last.end ? b.end : last.end);
                else
                    merged.Add(b);
            }

            var slot = TimeSpan.FromMinutes(Math.Max(5, slotIntervalMinutes));
            var cursor = RoundUpToInterval(open, slot);
            int count = 0;

            while (cursor.Add(slot) <= close)
            {
                var slotEnd = cursor.Add(slot);

                // Respect advance notice
                if (cursor < minStartBiz)
                {
                    cursor = cursor.Add(slot);
                    continue;
                }

                bool overlaps = false;
                foreach (var m in merged)
                {
                    if (slotEnd <= m.start) break;
                    if (cursor < m.end && slotEnd > m.start) { overlaps = true; break; }
                }

                if (!overlaps) count++;
                cursor = cursor.Add(slot);
            }

            return count;
        }

        private static DateTime RoundUpToInterval(DateTime dt, TimeSpan interval)
        {
            var ticks = interval.Ticks;
            var rounded = ((dt.Ticks + ticks - 1) / ticks) * ticks;
            return new DateTime(rounded, dt.Kind);
        }

        private static DateTime Max(DateTime a, DateTime b) => a > b ? a : b;
        private static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;

        private static string ToDuration(int minutes)
        {
            minutes = Math.Max(1, minutes);
            var ts = TimeSpan.FromMinutes(minutes);
            return $"{(int)ts.TotalHours:00}:{ts.Minutes:00}:{ts.Seconds:00}";
        }

        private string GetStatusColor(AppointmentStatus status)
        {
            return status switch
            {
                AppointmentStatus.Confirmed => "#10b981",
                AppointmentStatus.Pending => "#f59e0b",
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