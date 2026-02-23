using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SimplyAppoint.DataAccess.Repository.IRepository;
using SimplyAppoint.Models;
using SimplyAppoint.Models.Enums;
using SimplyAppoint.Models.ViewModels;
using System.Diagnostics;
using System.Globalization;

namespace SimplyAppointWeb.Controllers
{
    [Area("Admin")]
    [Authorize]
    public class AppointmentsController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserManager<IdentityUser> _userManager;

        public AppointmentsController(IUnitOfWork unitOfWork, UserManager<IdentityUser> userManager)
        {
            _unitOfWork = unitOfWork;
            _userManager = userManager;
        }

        // -------------------- INDEX --------------------
        public async Task<IActionResult> Index(string range = "week", string status = "all", string? q = null)
        {
            var (business, tz) = await GetBusinessContextAsync();
            if (business == null) return View(new AppointmentsIndexVM());

            var nowUtc = DateTimeOffset.UtcNow;
            var nowBiz = TimeZoneInfo.ConvertTime(nowUtc, tz);
            var todayBiz = nowBiz.Date;
            var weekAgoUtc = nowUtc.AddDays(-7);

            // Load appointments for THIS business
            var appts = _unitOfWork.Appointment
                .GetAll(a => a.BusinessId == business.Id, includeProperties: "Service")
                .ToList();

            // Filters
            appts = ApplyFilters(appts, tz, range, status, q);

            // Stats 
            var allAppts = _unitOfWork.Appointment
                .GetAll(a => a.BusinessId == business.Id, includeProperties: "Service")
                .ToList();

            var stats = BuildStats(allAppts, tz);

            // Upcoming next 3
            var upcoming = allAppts
                .Where(a => a.Status != AppointmentStatus.Cancelled && a.CancelledUtc == null)
                .Where(a => a.StartUtc >= nowUtc)
                .OrderBy(a => a.StartUtc)
                .Take(3)
                .Select(a =>
                {
                    var sBiz = TimeZoneInfo.ConvertTime(a.StartUtc, tz);
                    return new AppointmentsIndexVM.UpcomingApptVM
                    {
                        Id = a.Id,
                        Time = sBiz.ToString("HH:mm"),
                        Customer = DisplayCustomer(a),
                        Service = a.Service?.Name ?? "Service",
                        Status = a.Status
                    };
                })
                .ToList();

            var vm = new AppointmentsIndexVM
            {
                Query = q,
                DateRange = range,
                Status = status,

                TodayCount = stats.TodayCount,
                Next7DaysCount = stats.Next7DaysCount,
                RevenueWeek = stats.RevenueWeek,
                CancellationsWeek = stats.CancellationsWeek,

                TotalShown = appts.Count,

                Rows = appts
                    .OrderBy(a => a.StartUtc)
                    .Select(a =>
                    {
                        var startBiz = TimeZoneInfo.ConvertTime(a.StartUtc, tz);
                        var endBiz = TimeZoneInfo.ConvertTime(a.EndUtc, tz);

                        return new AppointmentsIndexVM.AppointmentRowVM
                        {
                            Id = a.Id,
                            WhenTime = $"{startBiz:HH:mm} – {endBiz:HH:mm}",
                            WhenDate = startBiz.ToString("ddd, dd MMM"),
                            CustomerDisplay = DisplayCustomer(a),
                            CustomerSub = string.IsNullOrWhiteSpace(a.Notes) ? "" : a.Notes!,
                            ServiceName = a.Service?.Name ?? "Service",
                            Status = a.Status
                        };
                    })
                    .ToList(),

                OpenSlotsCount = 0,
                Upcoming = upcoming
            };

            return View(vm);
        }

        // -------------------- CREATE --------------------
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var (business, tz) = await GetBusinessContextAsync();
            if (business == null) return RedirectToAction(nameof(Index));

            var nowBiz = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz);

            var vm = new AppointmentsVM
            {
                Date = nowBiz.ToString("yyyy-MM-dd"),
                StartTime = nowBiz.AddHours(1).ToString("HH:mm"),
                Status = AppointmentStatus.Confirmed
            };

            await PopulateServicesAsync(business.Id, vm);
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AppointmentsVM vm)
        {
            var (business, tz) = await GetBusinessContextAsync();
            if (business == null) return RedirectToAction(nameof(Index));

            await PopulateServicesAsync(business.Id, vm);

            if (!ModelState.IsValid)
                return View(vm);

            // Load service (required)
            var service = _unitOfWork.Service.Get(s => s.BusinessId == business.Id && s.Id == vm.ServiceId);
            if (service == null)
            {
                ModelState.AddModelError(nameof(vm.ServiceId), "Selected service does not exist.");
                return View(vm);
            }

            if (!TryParseLocalDateTime(vm.Date, vm.StartTime, out var startBiz))
            {
                ModelState.AddModelError("", "Invalid date/time.");
                return View(vm);
            }

            var duration = vm.DurationMinutes ?? service.DurationMinutes;
            var startUtc = ToUtc(startBiz, tz);
            var endUtc = startUtc.AddMinutes(duration);

            // Validate schedule constraints
            var validationError = ValidateSlot(
                businessId: business.Id,
                tz: tz,
                startBiz: startBiz,
                endBiz: TimeZoneInfo.ConvertTime(endUtc, tz).DateTime,
                service: service,
                excludeAppointmentId: null
            );

            if (validationError != null)
            {
                ModelState.AddModelError("", validationError);
                return View(vm);
            }

            var appt = new Appointment
            {
                BusinessId = business.Id,
                ServiceId = service.Id,

                CustomerName = vm.CustomerName?.Trim() ?? "",
                CustomerEmail = vm.CustomerEmail?.Trim() ?? "",
                CustomerPhone = string.IsNullOrWhiteSpace(vm.CustomerPhone) ? null : vm.CustomerPhone.Trim(),

                StartUtc = startUtc,
                EndUtc = endUtc,
                DurationMinutes = duration,

                Price = vm.PriceOverride ?? service.Price,
                Status = vm.Status,
                Notes = string.IsNullOrWhiteSpace(vm.Notes) ? null : vm.Notes.Trim(),
                CreatedUtc = DateTimeOffset.UtcNow
            };

            _unitOfWork.Appointment.Add(appt);
            _unitOfWork.Save();

            return RedirectToAction(nameof(Edit), new { id = appt.Id });
        }

        // -------------------- EDIT --------------------
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var (business, tz) = await GetBusinessContextAsync();
            if (business == null) return RedirectToAction(nameof(Index));

            var appt = _unitOfWork.Appointment.Get(a => a.BusinessId == business.Id && a.Id == id, includeProperties: "Service");
            if (appt == null) return NotFound();

            var startBiz = TimeZoneInfo.ConvertTime(appt.StartUtc, tz);
            var vm = new AppointmentsVM
            {
                Id = appt.Id,
                ServiceId = appt.ServiceId,
                CustomerName = appt.CustomerName ?? "",
                CustomerEmail = appt.CustomerEmail ?? "",
                CustomerPhone = appt.CustomerPhone,
                Status = appt.Status,
                Date = startBiz.ToString("yyyy-MM-dd"),
                StartTime = startBiz.ToString("HH:mm"),
                DurationMinutes = appt.DurationMinutes,
                PriceOverride = appt.Price,
                Notes = appt.Notes,
                ServiceLabel = appt.Service?.Name
            };

            await PopulateServicesAsync(business.Id, vm);
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, AppointmentsVM vm)
        {
            var (business, tz) = await GetBusinessContextAsync();
            if (business == null) return RedirectToAction(nameof(Index));

            await PopulateServicesAsync(business.Id, vm);

            if (!ModelState.IsValid)
                return View(vm);

            var appt = _unitOfWork.Appointment.Get(a => a.BusinessId == business.Id && a.Id == id, includeProperties: "Service");
            if (appt == null) return NotFound();

            var service = _unitOfWork.Service.Get(s => s.BusinessId == business.Id && s.Id == vm.ServiceId);
            if (service == null)
            {
                ModelState.AddModelError(nameof(vm.ServiceId), "Selected service does not exist.");
                return View(vm);
            }

            if (!TryParseLocalDateTime(vm.Date, vm.StartTime, out var startBiz))
            {
                ModelState.AddModelError("", "Invalid date/time.");
                return View(vm);
            }

            var duration = vm.DurationMinutes ?? service.DurationMinutes;
            var startUtc = ToUtc(startBiz, tz);
            var endUtc = startUtc.AddMinutes(duration);

            var validationError = ValidateSlot(
                businessId: business.Id,
                tz: tz,
                startBiz: startBiz,
                endBiz: TimeZoneInfo.ConvertTime(endUtc, tz).DateTime,
                service: service,
                excludeAppointmentId: appt.Id
            );

            if (validationError != null)
            {
                ModelState.AddModelError("", validationError);
                return View(vm);
            }

            // Update
            appt.ServiceId = service.Id;
            appt.CustomerName = vm.CustomerName?.Trim() ?? "";
            appt.CustomerEmail = vm.CustomerEmail?.Trim() ?? "";
            appt.CustomerPhone = string.IsNullOrWhiteSpace(vm.CustomerPhone) ? null : vm.CustomerPhone.Trim();

            appt.StartUtc = startUtc;
            appt.EndUtc = endUtc;
            appt.DurationMinutes = duration;

            appt.Price = vm.PriceOverride ?? service.Price;
            appt.Status = vm.Status;
            appt.Notes = string.IsNullOrWhiteSpace(vm.Notes) ? null : vm.Notes.Trim();

            // cancellation timestamp convention
            if (appt.Status == AppointmentStatus.Cancelled && appt.CancelledUtc == null)
                appt.CancelledUtc = DateTimeOffset.UtcNow;

            if (appt.Status != AppointmentStatus.Cancelled)
                appt.CancelledUtc = null;

            _unitOfWork.Appointment.Update(appt);
            _unitOfWork.Save();

            TempData["success"] = "Appointment updated.";
            return RedirectToAction(nameof(Edit), new { id = appt.Id });
        }

        // -------------------- HELPERS --------------------

        private async Task<(Business? business, TimeZoneInfo tz)> GetBusinessContextAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return (null, TimeZoneInfo.Local);

            var business = _unitOfWork.Business.Get(b => b.OwnerUserId == user.Id);
            if (business == null) return (null, TimeZoneInfo.Local);

            try
            {
                return (business, TimeZoneInfo.FindSystemTimeZoneById(business.TimeZoneId));
            }
            catch
            {
                return (business, TimeZoneInfo.Local);
            }
        }

        private async Task PopulateServicesAsync(int businessId, AppointmentsVM vm)
        {
            var services = _unitOfWork.Service
                .GetAll(s => s.BusinessId == businessId && s.IsActive)
                .OrderBy(s => s.Name)
                .ToList();

            vm.ServiceOptions = services
                .Select(s => new SelectListItem
                {
                    Value = s.Id.ToString(),
                    Text = $"{s.Name} ({s.DurationMinutes} min)",
                    Selected = (vm.ServiceId == s.Id)
                })
                .ToList();

            // prevent empty select from breaking UI
            if (!vm.ServiceOptions.Any())
            {
                vm.ServiceOptions.Add(new SelectListItem
                {
                    Value = "",
                    Text = "No services found (create a service first)",
                    Selected = true
                });
            }

            await Task.CompletedTask;
        }

        private static bool TryParseLocalDateTime(string date, string time, out DateTime local)
        {
            // date: yyyy-MM-dd, time: HH:mm
            local = default;

            if (string.IsNullOrWhiteSpace(date) || string.IsNullOrWhiteSpace(time))
                return false;

            var s = $"{date} {time}";
            return DateTime.TryParseExact(
                s,
                "yyyy-MM-dd HH:mm",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out local
            );
        }

        private static DateTimeOffset ToUtc(DateTime localBiz, TimeZoneInfo tz)
        {
            var unspecified = DateTime.SpecifyKind(localBiz, DateTimeKind.Unspecified);
            var utc = TimeZoneInfo.ConvertTimeToUtc(unspecified, tz);
            return new DateTimeOffset(utc, TimeSpan.Zero);
        }

        private static string DisplayCustomer(Appointment a)
        {
            if (!string.IsNullOrWhiteSpace(a.CustomerName)) return a.CustomerName!;
            if (!string.IsNullOrWhiteSpace(a.CustomerEmail)) return a.CustomerEmail!;
            return "Customer";
        }

        private static List<Appointment> ApplyFilters(List<Appointment> appts, TimeZoneInfo tz, string range, string status, string? q)
        {
            // date range filter 
            if (!string.Equals(range, "all", StringComparison.OrdinalIgnoreCase))
            {
                DateTime startBiz;
                DateTime endBiz;

                var nowBiz = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz).DateTime;
                var todayBiz = nowBiz.Date;

                switch (range.ToLowerInvariant())
                {
                    case "today":
                        startBiz = todayBiz;
                        endBiz = todayBiz.AddDays(1);
                        break;
                    case "month":
                        startBiz = new DateTime(todayBiz.Year, todayBiz.Month, 1);
                        endBiz = startBiz.AddMonths(1);
                        break;
                    default: // week
                        startBiz = todayBiz.AddDays(-(int)todayBiz.DayOfWeek + (int)DayOfWeek.Monday);
                        endBiz = startBiz.AddDays(7);
                        break;
                }

                appts = appts.Where(a =>
                {
                    var sBiz = TimeZoneInfo.ConvertTime(a.StartUtc, tz).DateTime;
                    return sBiz >= startBiz && sBiz < endBiz;
                }).ToList();
            }

            // status filter
            if (!string.Equals(status, "all", StringComparison.OrdinalIgnoreCase))
            {
                if (Enum.TryParse<AppointmentStatus>(status, true, out var st))
                {
                    appts = appts.Where(a => a.Status == st).ToList();
                }
            }

            // search filter
            if (!string.IsNullOrWhiteSpace(q))
            {
                q = q.Trim();
                appts = appts.Where(a =>
                    (a.CustomerName ?? "").Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    (a.CustomerEmail ?? "").Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    (a.Service?.Name ?? "").Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    (a.Notes ?? "").Contains(q, StringComparison.OrdinalIgnoreCase)
                ).ToList();
            }

            return appts;
        }

        private (int TodayCount, int Next7DaysCount, decimal RevenueWeek, int CancellationsWeek) BuildStats(List<Appointment> appts, TimeZoneInfo tz)
        {
            var nowUtc = DateTimeOffset.UtcNow;
            var nowBiz = TimeZoneInfo.ConvertTime(nowUtc, tz);
            var todayBiz = nowBiz.Date;
            var next7BizEnd = todayBiz.AddDays(7);

            int todayCount = appts.Count(a =>
            {
                var sBiz = TimeZoneInfo.ConvertTime(a.StartUtc, tz);
                return a.Status != AppointmentStatus.Cancelled && a.CancelledUtc == null && sBiz.Date == todayBiz;
            });

            int next7 = appts.Count(a =>
            {
                var sBiz = TimeZoneInfo.ConvertTime(a.StartUtc, tz).DateTime;
                return a.Status != AppointmentStatus.Cancelled && a.CancelledUtc == null && sBiz >= todayBiz && sBiz < next7BizEnd;
            });

            // revenue (week) - confirmed only, non-cancelled
            decimal revenueWeek = appts
                .Where(a => a.Status == AppointmentStatus.Confirmed && a.CancelledUtc == null)
                .Where(a =>
                {
                    var sBiz = TimeZoneInfo.ConvertTime(a.StartUtc, tz).DateTime;
                    return sBiz >= todayBiz && sBiz < next7BizEnd;
                })
                .Sum(a => a.Price);

            int cancellationsWeek = appts.Count(a =>
                a.Status == AppointmentStatus.Cancelled || a.CancelledUtc != null
            );

            return (todayCount, next7, revenueWeek, cancellationsWeek);
        }

        private string? ValidateSlot(
            int businessId,
            TimeZoneInfo tz,
            DateTime startBiz,
            DateTime endBiz,
            Service service,
            int? excludeAppointmentId)
        {
            if (endBiz <= startBiz)
                return "End time must be after start time.";

            var nowBiz = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz).DateTime;
            if (startBiz < nowBiz.AddMinutes(-1))
                return "Start time must be in the future.";

            // Working hours check 
            var weekday = startBiz.DayOfWeek switch
            {
                DayOfWeek.Monday => Weekday.Monday,
                DayOfWeek.Tuesday => Weekday.Tuesday,
                DayOfWeek.Wednesday => Weekday.Wednesday,
                DayOfWeek.Thursday => Weekday.Thursday,
                DayOfWeek.Friday => Weekday.Friday,
                DayOfWeek.Saturday => Weekday.Saturday,
                DayOfWeek.Sunday => Weekday.Sunday,
                _ => Weekday.Monday
            };

            var wh = _unitOfWork.WorkingHours.Get(w => w.BusinessId == businessId && w.Weekday == weekday);
            if (wh != null && (!wh.IsClosed) && wh.OpenTime.HasValue && wh.CloseTime.HasValue)
            {
                var open = startBiz.Date.Add(wh.OpenTime.Value.ToTimeSpan());
                var close = startBiz.Date.Add(wh.CloseTime.Value.ToTimeSpan());

                if (startBiz < open || endBiz > close)
                    return $"Outside working hours ({open:HH:mm}–{close:HH:mm}).";
            }

            // TimeOff overlap 
            var timeOffs = _unitOfWork.TimeOff.GetAll(t => t.BusinessId == businessId).ToList();
            foreach (var t in timeOffs)
            {
                var s = TimeZoneInfo.ConvertTime(t.StartUtc, tz).DateTime;
                var e = TimeZoneInfo.ConvertTime(t.EndUtc, tz).DateTime;
                if (Overlaps(startBiz, endBiz, s, e))
                    return "This time overlaps with Time Off.";
            }

            // Appointment overlap with buffers
            var before = service.BufferBefore;
            var after = service.BufferAfter;

            var blockStart = startBiz.AddMinutes(-before);
            var blockEnd = endBiz.AddMinutes(after);

            var appts = _unitOfWork.Appointment
                .GetAll(a => a.BusinessId == businessId && a.CancelledUtc == null && a.Status != AppointmentStatus.Cancelled, includeProperties: "Service")
                .ToList();

            foreach (var a in appts)
            {
                if (excludeAppointmentId.HasValue && a.Id == excludeAppointmentId.Value)
                    continue;

                var s0 = TimeZoneInfo.ConvertTime(a.StartUtc, tz).DateTime;
                var e0 = TimeZoneInfo.ConvertTime(a.EndUtc, tz).DateTime;

                var s = s0.AddMinutes(-(a.Service?.BufferBefore ?? 0));
                var e = e0.AddMinutes((a.Service?.BufferAfter ?? 0));

                if (Overlaps(blockStart, blockEnd, s, e))
                    return "This time overlaps with an existing appointment.";
            }

            return null;
        }

        private static bool Overlaps(DateTime aStart, DateTime aEnd, DateTime bStart, DateTime bEnd)
            => aStart < bEnd && aEnd > bStart;

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}