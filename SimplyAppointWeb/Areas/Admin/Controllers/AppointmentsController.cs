using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SimplyAppoint.DataAccess.Repository.IRepository;
using SimplyAppoint.Models;
using SimplyAppoint.Models.Enums;
using SimplyAppoint.Models.ViewModels;
using System.Diagnostics;
using System.Globalization;
using System.Net;

namespace SimplyAppointWeb.Controllers
{
    [Area("Admin")]
    [Authorize]
    public class AppointmentsController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IEmailSender _emailSender;

        public AppointmentsController(
            IUnitOfWork unitOfWork,
            UserManager<IdentityUser> userManager,
            IEmailSender emailSender)
        {
            _unitOfWork = unitOfWork;
            _userManager = userManager;
            _emailSender = emailSender;
        }

        // -------------------- INDEX --------------------
        public async Task<IActionResult> Index(string range = "all", string status = "all", string? q = null)
        {
            var (business, tz) = await GetBusinessContextAsync();
            if (business == null) return View(new AppointmentsIndexVM());

            var nowUtc = DateTimeOffset.UtcNow;

            var allAppts = _unitOfWork.Appointment
                .GetAll(a => a.BusinessId == business.Id, includeProperties: "Service")
                .ToList();

            var completedChanged = AutoCompletePastConfirmed(allAppts, nowUtc);
            if (completedChanged)
                _unitOfWork.Save();

            var filtered = ApplyFilters(allAppts, tz, range, status, q);
            var stats = BuildStats(allAppts, tz);

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

                TotalShown = filtered.Count,

                Rows = filtered
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
            PopulateBookingPolicy(business.Id, vm);
            await PopulateAvailabilityAsync(business, tz, vm);

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AppointmentsVM vm)
        {
            var (business, tz) = await GetBusinessContextAsync();
            if (business == null) return RedirectToAction(nameof(Index));

            await PopulateServicesAsync(business.Id, vm);
            PopulateBookingPolicy(business.Id, vm);
            await PopulateAvailabilityAsync(business, tz, vm);

            if (vm.Status != AppointmentStatus.Confirmed)
                ModelState.AddModelError(nameof(vm.Status), "New appointments can only be created as Confirmed.");

            if (!ModelState.IsValid)
                return View(vm);

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

                CustomerName = (vm.CustomerName ?? "").Trim(),
                CustomerEmail = (vm.CustomerEmail ?? "").Trim(),
                CustomerPhone = string.IsNullOrWhiteSpace(vm.CustomerPhone) ? null : vm.CustomerPhone.Trim(),

                StartUtc = startUtc,
                EndUtc = endUtc,
                DurationMinutes = duration,

                Price = vm.PriceOverride ?? service.Price,
                Status = AppointmentStatus.Confirmed,
                Notes = string.IsNullOrWhiteSpace(vm.Notes) ? null : vm.Notes.Trim(),
                CreatedUtc = DateTimeOffset.UtcNow,

                ConfirmationToken = Guid.NewGuid().ToString()
            };

            _unitOfWork.Appointment.Add(appt);
            _unitOfWork.Save();

            if (!string.IsNullOrWhiteSpace(appt.CustomerEmail))
            {
                try
                {
                    var apptWithNav = _unitOfWork.Appointment.Get(
                        a => a.Id == appt.Id,
                        includeProperties: "Business,Service"
                    );

                    if (apptWithNav != null)
                        await SendAdminCreatedAppointmentEmail(apptWithNav, tz);
                }
                catch
                {
                    TempData["error"] = "Appointment saved, but email could not be sent.";
                }
            }

            TempData["success"] = "Appointment created.";
            return RedirectToAction(nameof(Index));
        }

        // -------------------- EDIT --------------------
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var (business, tz) = await GetBusinessContextAsync();
            if (business == null) return RedirectToAction(nameof(Index));

            var appt = _unitOfWork.Appointment.Get(
                a => a.BusinessId == business.Id && a.Id == id,
                includeProperties: "Service"
            );
            if (appt == null) return NotFound();

            var changed = AutoCompletePastConfirmed(new List<Appointment> { appt }, DateTimeOffset.UtcNow);
            if (changed) _unitOfWork.Save();

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
            PopulateBookingPolicy(business.Id, vm);

            var ownerCreated = IsOwnerCreated(appt);

            if (ownerCreated)
            {
                await PopulateAvailabilityAsync(business, tz, vm);
            }
            else
            {
                vm.AvailableTimes = new List<string>();
                vm.AvailabilityMessage = "This appointment was booked by a customer. Time and price cannot be changed.";
            }

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, AppointmentsVM vm)
        {
            var (business, tz) = await GetBusinessContextAsync();
            if (business == null) return RedirectToAction(nameof(Index));

            var appt = _unitOfWork.Appointment.Get(
                a => a.BusinessId == business.Id && a.Id == id,
                includeProperties: "Service,Business"
            );
            if (appt == null) return NotFound();

            var changedAuto = AutoCompletePastConfirmed(new List<Appointment> { appt }, DateTimeOffset.UtcNow);
            if (changedAuto) _unitOfWork.Save();

            if (appt.Status == AppointmentStatus.Cancelled ||
                appt.Status == AppointmentStatus.Completed ||
                appt.Status == AppointmentStatus.NoShow ||
                appt.Status == AppointmentStatus.Pending)
            {
                TempData["error"] = "This appointment is locked and cannot be edited.";
                return RedirectToAction(nameof(Edit), new { id });
            }

            await PopulateServicesAsync(business.Id, vm);
            PopulateBookingPolicy(business.Id, vm);

            var ownerCreated = IsOwnerCreated(appt);

            if (vm.Status == AppointmentStatus.Pending || vm.Status == AppointmentStatus.Completed)
                ModelState.AddModelError(nameof(vm.Status), "You cannot set Pending or Completed manually.");

            if (!ownerCreated)
            {
                vm.ServiceId = appt.ServiceId;
                vm.CustomerName = appt.CustomerName ?? "";
                vm.CustomerEmail = appt.CustomerEmail ?? "";
                vm.CustomerPhone = appt.CustomerPhone;
                vm.Date = TimeZoneInfo.ConvertTime(appt.StartUtc, tz).ToString("yyyy-MM-dd");
                vm.StartTime = TimeZoneInfo.ConvertTime(appt.StartUtc, tz).ToString("HH:mm");
                vm.DurationMinutes = appt.DurationMinutes;
                vm.PriceOverride = appt.Price;

                vm.AvailableTimes = new List<string>();
                vm.AvailabilityMessage = "This appointment was booked by a customer. Time and price cannot be changed.";

                if (vm.Status != AppointmentStatus.Confirmed && vm.Status != AppointmentStatus.Cancelled)
                    ModelState.AddModelError(nameof(vm.Status), "For customer bookings you can only Confirm or Cancel.");
            }
            else
            {
                await PopulateAvailabilityAsync(business, tz, vm);

                if (vm.Status != AppointmentStatus.Confirmed && vm.Status != AppointmentStatus.Cancelled)
                    ModelState.AddModelError(nameof(vm.Status), "You can only set Confirmed or Cancelled here.");
            }

            if (!ModelState.IsValid)
                return View(vm);

            var prevStartUtc = appt.StartUtc;
            var prevEndUtc = appt.EndUtc;
            var prevPrice = appt.Price;
            var prevServiceId = appt.ServiceId;

            var oldStatus = appt.Status;

            appt.Status = vm.Status;

            if (appt.Status == AppointmentStatus.Cancelled)
            {
                if (appt.CancelledUtc == null)
                    appt.CancelledUtc = DateTimeOffset.UtcNow;
            }
            else
            {
                appt.CancelledUtc = null;
            }

            appt.Notes = string.IsNullOrWhiteSpace(vm.Notes) ? null : vm.Notes.Trim();

            bool changedTimeOrPriceOrService = false;

            if (ownerCreated && appt.Status != AppointmentStatus.Cancelled)
            {
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

                appt.ServiceId = service.Id;
                appt.StartUtc = startUtc;
                appt.EndUtc = endUtc;
                appt.DurationMinutes = duration;

                appt.Price = vm.PriceOverride ?? service.Price;

                appt.CustomerName = (vm.CustomerName ?? "").Trim();
                appt.CustomerEmail = (vm.CustomerEmail ?? "").Trim();
                appt.CustomerPhone = string.IsNullOrWhiteSpace(vm.CustomerPhone) ? null : vm.CustomerPhone.Trim();

                changedTimeOrPriceOrService =
                    appt.StartUtc != prevStartUtc ||
                    appt.EndUtc != prevEndUtc ||
                    appt.Price != prevPrice ||
                    appt.ServiceId != prevServiceId;
            }

            _unitOfWork.Appointment.Update(appt);
            _unitOfWork.Save();

            if (oldStatus != AppointmentStatus.Cancelled && appt.Status == AppointmentStatus.Cancelled)
            {
                if (!string.IsNullOrWhiteSpace(appt.CustomerEmail))
                {
                    try
                    {
                        var apptWithNav = _unitOfWork.Appointment.Get(
                            a => a.Id == appt.Id,
                            includeProperties: "Business,Service"
                        );
                        if (apptWithNav != null)
                            await SendCancelledEmail(apptWithNav, tz);
                    }
                    catch { }
                }
            }

            if (ownerCreated &&
                changedTimeOrPriceOrService &&
                appt.Status == AppointmentStatus.Confirmed &&
                !string.IsNullOrWhiteSpace(appt.CustomerEmail))
            {
                try
                {
                    var apptWithNav = _unitOfWork.Appointment.Get(
                        a => a.Id == appt.Id,
                        includeProperties: "Business,Service"
                    );
                    if (apptWithNav != null)
                        await SendChangedEmail(apptWithNav, tz, prevStartUtc, prevEndUtc, prevPrice);
                }
                catch { }
            }

            TempData["success"] = "Appointment updated.";
            return RedirectToAction(nameof(Index));
        }

        // -------------------- QUICK ACTIONS --------------------

        // Cancel permanently
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            var (business, tz) = await GetBusinessContextAsync();
            if (business == null) return RedirectToAction(nameof(Index));

            var appt = _unitOfWork.Appointment.Get(
                a => a.BusinessId == business.Id && a.Id == id,
                includeProperties: "Business,Service"
            );
            if (appt == null) return NotFound();

            if (appt.Status == AppointmentStatus.Cancelled)
            {
                TempData["error"] = "Already cancelled.";
                return RedirectToAction(nameof(Index));
            }

            if (appt.Status == AppointmentStatus.NoShow ||
                appt.Status == AppointmentStatus.Completed ||
                appt.Status == AppointmentStatus.Pending)
            {
                TempData["error"] = "This appointment is locked and cannot be cancelled.";
                return RedirectToAction(nameof(Index));
            }

            appt.Status = AppointmentStatus.Cancelled;
            appt.CancelledUtc = DateTimeOffset.UtcNow;

            _unitOfWork.Appointment.Update(appt);
            _unitOfWork.Save();

            if (!string.IsNullOrWhiteSpace(appt.CustomerEmail))
            {
                try { await SendCancelledEmail(appt, tz); } catch { }
            }

            TempData["success"] = "Appointment cancelled permanently.";
            return RedirectToAction(nameof(Index));
        }

        // NoShow
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkNoShow(int id)
        {
            var (business, tz) = await GetBusinessContextAsync();
            if (business == null) return RedirectToAction(nameof(Index));

            var appt = _unitOfWork.Appointment.Get(
                a => a.BusinessId == business.Id && a.Id == id,
                includeProperties: "Business,Service"
            );
            if (appt == null) return NotFound();

            if (appt.Status == AppointmentStatus.Cancelled)
            {
                TempData["error"] = "Cancelled appointments cannot be marked as No-Show.";
                return RedirectToAction(nameof(Index));
            }

            if (appt.Status == AppointmentStatus.Pending)
            {
                TempData["error"] = "Pending appointments cannot be marked as No-Show.";
                return RedirectToAction(nameof(Index));
            }

            if (appt.Status == AppointmentStatus.NoShow)
            {
                TempData["error"] = "This appointment is already marked as No-Show.";
                return RedirectToAction(nameof(Index));
            }

            var nowUtc = DateTimeOffset.UtcNow;
            var changed = AutoCompletePastConfirmed(new List<Appointment> { appt }, nowUtc);
            if (changed) _unitOfWork.Save();

            if (appt.Status != AppointmentStatus.Completed || appt.EndUtc > nowUtc)
            {
                TempData["error"] = "You can only mark an appointment as No-Show after it has ended (Completed).";
                return RedirectToAction(nameof(Index));
            }

            appt.Status = AppointmentStatus.NoShow;

            _unitOfWork.Appointment.Update(appt);
            _unitOfWork.Save();

            TempData["success"] = "Marked as No-Show.";
            return RedirectToAction(nameof(Index));
        }

        // Hard delete 
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteHard(int id)
        {
            var (business, tz) = await GetBusinessContextAsync();
            if (business == null) return RedirectToAction(nameof(Index));

            var appt = _unitOfWork.Appointment.Get(
                a => a.BusinessId == business.Id && a.Id == id,
                includeProperties: "Business,Service"
            );
            if (appt == null) return NotFound();

            if (appt.Status == AppointmentStatus.Cancelled ||
                appt.Status == AppointmentStatus.Completed ||
                appt.Status == AppointmentStatus.NoShow ||
                appt.Status == AppointmentStatus.Pending)
            {
                TempData["error"] = "This appointment is locked and cannot be deleted.";
                return RedirectToAction(nameof(Index));
            }

            var startBiz = TimeZoneInfo.ConvertTime(appt.StartUtc, tz).DateTime;
            var nowBiz = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz).DateTime;
            if (startBiz <= nowBiz)
            {
                TempData["error"] = "Past/started appointments cannot be hard deleted.";
                return RedirectToAction(nameof(Index));
            }

            if (!string.IsNullOrWhiteSpace(appt.CustomerEmail))
            {
                try { await SendCancelledEmail(appt, tz); } catch { }
            }

            _unitOfWork.Appointment.Remove(appt);
            _unitOfWork.Save();

            TempData["success"] = "Appointment deleted permanently.";
            return RedirectToAction(nameof(Index));
        }

        // -------------------- AVAILABILITY --------------------
        [HttpGet]
        public async Task<IActionResult> GetAvailableSlots(int serviceId, string date)
        {
            var (business, tz) = await GetBusinessContextAsync();
            if (business == null) return Json(new List<string>());

            if (!DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                return Json(new List<string>());

            var slots = ComputeAvailableSlots(business.Id, serviceId, d, tz);
            return Json(slots);
        }

        // -------------------- EMAIL HELPERS --------------------

        private async Task SendAdminCreatedAppointmentEmail(Appointment appt, TimeZoneInfo tz)
        {
            var businessName = appt.Business?.Name ?? "Business";
            var serviceName = appt.Service?.Name ?? "Service";

            var startBiz = TimeZoneInfo.ConvertTime(appt.StartUtc, tz);
            var endBiz = TimeZoneInfo.ConvertTime(appt.EndUtc, tz);

            string googleUrl = GenerateGoogleCalendarLink(appt);
            string appleUrl = Url.Action("DownloadIcs", "Home", new { area = "Customer", appointmentId = appt.Id }, protocol: Request.Scheme) ?? "";
            string cancelUrl = Url.Action("CancelAppointment", "Home", new { area = "Customer", token = appt.ConfirmationToken }, protocol: Request.Scheme) ?? "";

            var subject = $"New appointment at {businessName}";
            var safeName = !string.IsNullOrWhiteSpace(appt.CustomerName) ? appt.CustomerName : "there";

            string html = $@"
<div style='background-color:#f4f7fa;padding:40px 20px;font-family:sans-serif;'>
  <div style='max-width:560px;margin:0 auto;background:#ffffff;border-radius:16px;overflow:hidden;box-shadow:0 4px 20px rgba(0,0,0,0.10);'>
    <div style='background:#0d6efd;padding:26px;text-align:center;color:#ffffff;'>
      <h2 style='margin:0;'>Appointment booked</h2>
    </div>
    <div style='padding:30px;'>
      <p style='margin-top:0;'>Hello {WebUtility.HtmlEncode(safeName)},</p>
      <p>A new appointment has been scheduled for you at <strong>{WebUtility.HtmlEncode(businessName)}</strong>.</p>

      <div style='border-left:4px solid #0d6efd;background:#f8fafc;padding:18px;border-radius:0 12px 12px 0;margin:18px 0;'>
        <table style='width:100%;font-size:14px;'>
          <tr><td><strong>Service:</strong></td><td style='text-align:right;'>{WebUtility.HtmlEncode(serviceName)}</td></tr>
          <tr><td><strong>Date:</strong></td><td style='text-align:right;'>{startBiz:dd.MM.yyyy}</td></tr>
          <tr><td><strong>Time:</strong></td><td style='text-align:right;'>{startBiz:HH:mm} – {endBiz:HH:mm}</td></tr>
          <tr><td><strong>Price:</strong></td><td style='text-align:right;font-weight:bold;'>{appt.Price:0.00} €</td></tr>
        </table>
      </div>

      <div style='text-align:center;margin-top:18px;'>
        <p style='font-weight:bold;color:#666;margin-bottom:12px;'>Add to your calendar:</p>
        <a href='{googleUrl}' target='_blank' style='display:inline-block;padding:12px 22px;margin:5px;border:1px solid #db4437;text-decoration:none;border-radius:8px;font-weight:bold;color:#db4437;background:#fff;'>Google</a>
        <a href='{appleUrl}' style='display:inline-block;padding:12px 22px;margin:5px;border:1px solid #000;text-decoration:none;border-radius:8px;font-weight:bold;color:#000;background:#fff;'>Apple / Outlook</a>
      </div>

      <div style='text-align:center;margin-top:22px;padding-top:18px;border-top:1px solid #eee;'>
        <p style='font-size:12px;color:#888;margin:0 0 10px;'>Can&apos;t make it?</p>
        <a href='{cancelUrl}' style='display:inline-block;background:#dc3545;color:#fff;padding:12px 18px;border-radius:10px;text-decoration:none;font-weight:700;'>Cancel appointment</a>
        <div style='font-size:11px;color:#aaa;margin-top:10px;'>Cancellation may be subject to the business policy.</div>
      </div>
    </div>
  </div>
</div>";

            await _emailSender.SendEmailAsync(appt.CustomerEmail, subject, html);
        }

        private async Task SendCancelledEmail(Appointment appt, TimeZoneInfo tz)
        {
            var businessName = appt.Business?.Name ?? "Business";
            var serviceName = appt.Service?.Name ?? "Service";
            var startBiz = TimeZoneInfo.ConvertTime(appt.StartUtc, tz);

            string html = $@"
<div style='background-color:#f4f7fa;padding:40px 20px;font-family:sans-serif;'>
  <div style='max-width:520px;margin:0 auto;background:#ffffff;border-radius:16px;overflow:hidden;box-shadow:0 4px 20px rgba(0,0,0,0.10);'>
    <div style='background:#dc3545;padding:22px;text-align:center;color:#ffffff;'>
      <h2 style='margin:0;'>Appointment cancelled</h2>
    </div>
    <div style='padding:26px;'>
      <p>Your appointment at <strong>{WebUtility.HtmlEncode(businessName)}</strong> has been cancelled.</p>
      <div style='background:#f8fafc;padding:14px;border-radius:12px;'>
        <div><strong>Service:</strong> {WebUtility.HtmlEncode(serviceName)}</div>
        <div><strong>Date:</strong> {startBiz:dd.MM.yyyy}</div>
        <div><strong>Time:</strong> {startBiz:HH:mm}</div>
      </div>
    </div>
  </div>
</div>";

            await _emailSender.SendEmailAsync(appt.CustomerEmail, $"Cancelled: {businessName}", html);
        }

        private async Task SendChangedEmail(Appointment appt, TimeZoneInfo tz, DateTimeOffset prevStartUtc, DateTimeOffset prevEndUtc, decimal prevPrice)
        {
            var businessName = appt.Business?.Name ?? "Business";
            var serviceName = appt.Service?.Name ?? "Service";

            var startBizNew = TimeZoneInfo.ConvertTime(appt.StartUtc, tz);
            var endBizNew = TimeZoneInfo.ConvertTime(appt.EndUtc, tz);

            var startBizOld = TimeZoneInfo.ConvertTime(prevStartUtc, tz);
            var endBizOld = TimeZoneInfo.ConvertTime(prevEndUtc, tz);

            var subject = $"Updated appointment at {businessName}";
            var safeName = !string.IsNullOrWhiteSpace(appt.CustomerName) ? appt.CustomerName : "there";

            string html = $@"
<div style='background-color:#f4f7fa;padding:40px 20px;font-family:sans-serif;'>
  <div style='max-width:560px;margin:0 auto;background:#ffffff;border-radius:16px;overflow:hidden;box-shadow:0 4px 20px rgba(0,0,0,0.10);'>
    <div style='background:#0d6efd;padding:26px;text-align:center;color:#ffffff;'>
      <h2 style='margin:0;'>Appointment updated</h2>
    </div>
    <div style='padding:30px;'>
      <p style='margin-top:0;'>Hello {WebUtility.HtmlEncode(safeName)},</p>
      <p>Your appointment at <strong>{WebUtility.HtmlEncode(businessName)}</strong> has been updated.</p>

      <div style='background:#f8fafc;padding:16px;border-radius:12px;margin:16px 0;'>
        <div style='font-weight:700;margin-bottom:8px;'>New details</div>
        <div><strong>Service:</strong> {WebUtility.HtmlEncode(serviceName)}</div>
        <div><strong>Date:</strong> {startBizNew:dd.MM.yyyy}</div>
        <div><strong>Time:</strong> {startBizNew:HH:mm} – {endBizNew:HH:mm}</div>
        <div><strong>Price:</strong> {appt.Price:0.00} €</div>
      </div>

      <div style='background:#fff3cd;padding:14px;border-radius:12px;border:1px solid #ffe69c;'>
        <div style='font-weight:700;margin-bottom:6px;'>Previous details</div>
        <div><strong>Date:</strong> {startBizOld:dd.MM.yyyy}</div>
        <div><strong>Time:</strong> {startBizOld:HH:mm} – {endBizOld:HH:mm}</div>
        <div><strong>Price:</strong> {prevPrice:0.00} €</div>
      </div>

      <p style='margin:18px 0 0;color:#888;font-size:12px;'>
        If this change doesn&apos;t work for you, please use the cancellation link from your original booking email or contact the business.
      </p>
    </div>
  </div>
</div>";

            await _emailSender.SendEmailAsync(appt.CustomerEmail, subject, html);
        }

        private string GenerateGoogleCalendarLink(Appointment app)
        {
            var start = app.StartUtc.ToString("yyyyMMddTHHmmssZ");
            var end = app.EndUtc.ToString("yyyyMMddTHHmmssZ");

            string serviceName = app.Service?.Name ?? "Appointment";
            string businessName = app.Business?.Name ?? "Business";
            var details = $"Service: {serviceName}. Price: {app.Price:0.00} EUR.";

            return $"https://www.google.com/calendar/render?action=TEMPLATE" +
                   $"&text={Uri.EscapeDataString("Booking: " + serviceName)}" +
                   $"&dates={start}/{end}" +
                   $"&details={Uri.EscapeDataString(details)}" +
                   $"&location={Uri.EscapeDataString(businessName)}&sf=true&output=xml";
        }

        // -------------------- POLICY + AVAILABILITY --------------------
        private void PopulateBookingPolicy(int businessId, AppointmentsVM vm)
        {
            var policy = _unitOfWork.BookingPolicy.Get(p => p.BusinessId == businessId);
            if (policy == null) return;

            vm.SlotIntervalMinutes = policy.SlotIntervalMinutes;
            vm.AdvanceNoticeMinutes = policy.AdvanceNoticeMinutes;
            vm.CancellationWindowMinutes = policy.CancellationWindowMinutes;
            vm.MaxAdvanceDays = policy.MaxAdvanceDays;
        }

        private async Task PopulateAvailabilityAsync(Business business, TimeZoneInfo tz, AppointmentsVM vm)
        {
            vm.AvailableTimes = new List<string>();
            vm.AvailabilityMessage = null;

            if (vm.ServiceId <= 0 || string.IsNullOrWhiteSpace(vm.Date))
            {
                vm.AvailabilityMessage = "Select a service and date to load available times.";
                return;
            }

            if (!DateTime.TryParseExact(vm.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            {
                vm.AvailabilityMessage = "Invalid date.";
                return;
            }

            var slots = ComputeAvailableSlots(business.Id, vm.ServiceId, d, tz);

            if (!slots.Any())
            {
                vm.AvailabilityMessage = "No available times for the selected date/service.";
                return;
            }

            vm.AvailableTimes = slots;
            await Task.CompletedTask;
        }

        private List<string> ComputeAvailableSlots(int businessId, int serviceId, DateTime date, TimeZoneInfo tz)
        {
            var service = _unitOfWork.Service.Get(s => s.Id == serviceId && s.BusinessId == businessId);
            if (service == null) return new List<string>();

            var business = _unitOfWork.Business.Get(b => b.Id == businessId, includeProperties: "WorkingHours");
            var policy = _unitOfWork.BookingPolicy.Get(p => p.BusinessId == businessId);
            if (business == null || policy == null) return new List<string>();

            int dayNum = (int)date.DayOfWeek;
            if (dayNum == 0) dayNum = 7;

            var workingDay = business.WorkingHours.FirstOrDefault(wh => (int)wh.Weekday == dayNum && !wh.IsClosed);
            if (workingDay == null || !workingDay.OpenTime.HasValue || !workingDay.CloseTime.HasValue)
                return new List<string>();

            var existingBookings = _unitOfWork.Appointment
                .GetAll(a =>
                        a.BusinessId == businessId &&
                        a.Status != AppointmentStatus.Cancelled &&
                        a.CancelledUtc == null,
                    includeProperties: "Service")
                .ToList()
                .Where(a =>
                {
                    var startLocal = TimeZoneInfo.ConvertTime(a.StartUtc, tz).DateTime.Date;
                    return startLocal == date.Date;
                })
                .ToList();

            var openLocal = date.Date.Add(workingDay.OpenTime.Value.ToTimeSpan());
            var closeLocal = date.Date.Add(workingDay.CloseTime.Value.ToTimeSpan());

            int interval = policy.SlotIntervalMinutes;
            var nowLocal = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz).DateTime;

            var availableSlots = new List<string>();
            var cur = openLocal;

            while (cur.AddMinutes(service.DurationMinutes) <= closeLocal)
            {
                var curEnd = cur.AddMinutes(service.DurationMinutes);

                bool isPast = cur < nowLocal;
                bool satisfiesNotice = cur >= nowLocal.AddMinutes(policy.AdvanceNoticeMinutes);

                var curBlockStart = cur.AddMinutes(-service.BufferBefore);
                var curBlockEnd = curEnd.AddMinutes(service.BufferAfter);

                bool occupied = existingBookings.Any(b =>
                {
                    var bStart = TimeZoneInfo.ConvertTime(b.StartUtc, tz).DateTime;
                    var bEnd = TimeZoneInfo.ConvertTime(b.EndUtc, tz).DateTime;

                    var bBlockStart = bStart.AddMinutes(-(b.Service?.BufferBefore ?? 0));
                    var bBlockEnd = bEnd.AddMinutes((b.Service?.BufferAfter ?? 0));

                    return curBlockStart < bBlockEnd && curBlockEnd > bBlockStart;
                });

                if (!occupied && !isPast && satisfiesNotice)
                    availableSlots.Add(cur.ToString("HH:mm"));

                cur = cur.AddMinutes(interval);
            }

            return availableSlots;
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
            local = default;
            if (string.IsNullOrWhiteSpace(date) || string.IsNullOrWhiteSpace(time)) return false;

            var s = $"{date} {time}";
            return DateTime.TryParseExact(s, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out local);
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
            if (!string.Equals(range, "all", StringComparison.OrdinalIgnoreCase))
            {
                DateTime startBiz;
                DateTime endBiz;

                var todayBiz = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz).Date;

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
                    default:
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

            if (!string.Equals(status, "all", StringComparison.OrdinalIgnoreCase))
            {
                if (Enum.TryParse<AppointmentStatus>(status, true, out var st))
                    appts = appts.Where(a => a.Status == st).ToList();
            }

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
            var todayBiz = TimeZoneInfo.ConvertTime(nowUtc, tz).Date;

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

            var weekStartBiz = todayBiz.AddDays(-(int)todayBiz.DayOfWeek + (int)DayOfWeek.Monday);
            var weekEndBiz = weekStartBiz.AddDays(7);

            bool Earned(Appointment a)
            {
                if (a.CancelledUtc != null || a.Status == AppointmentStatus.Cancelled) return false;
                if (a.Status == AppointmentStatus.NoShow) return false;
                if (a.Status == AppointmentStatus.Completed) return true;
                return a.Status == AppointmentStatus.Confirmed && a.EndUtc <= nowUtc;
            }

            decimal revenueWeek = appts
                .Where(Earned)
                .Where(a =>
                {
                    var startBiz = TimeZoneInfo.ConvertTime(a.StartUtc, tz).DateTime;
                    return startBiz >= weekStartBiz && startBiz < weekEndBiz;
                })
                .Sum(a => a.Price);

            int cancellationsWeek = appts.Count(a =>
            {
                if (a.Status != AppointmentStatus.Cancelled && a.CancelledUtc == null) return false;

                var cUtc = a.CancelledUtc ?? a.CreatedUtc;
                var cBiz = TimeZoneInfo.ConvertTime(cUtc, tz).DateTime;
                return cBiz >= weekStartBiz && cBiz < weekEndBiz;
            });

            return (todayCount, next7, revenueWeek, cancellationsWeek);
        }

        private string? ValidateSlot(int businessId, TimeZoneInfo tz, DateTime startBiz, DateTime endBiz, Service service, int? excludeAppointmentId)
        {
            if (endBiz <= startBiz) return "End time must be after start time.";

            var nowBiz = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz).DateTime;
            if (startBiz < nowBiz.AddMinutes(-1)) return "Start time must be in the future.";

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
            if (wh != null && !wh.IsClosed && wh.OpenTime.HasValue && wh.CloseTime.HasValue)
            {
                var open = startBiz.Date.Add(wh.OpenTime.Value.ToTimeSpan());
                var close = startBiz.Date.Add(wh.CloseTime.Value.ToTimeSpan());
                if (startBiz < open || endBiz > close) return $"Outside working hours ({open:HH:mm}–{close:HH:mm}).";
            }

            var timeOffs = _unitOfWork.TimeOff.GetAll(t => t.BusinessId == businessId).ToList();
            foreach (var t in timeOffs)
            {
                var s = TimeZoneInfo.ConvertTime(t.StartUtc, tz).DateTime;
                var e = TimeZoneInfo.ConvertTime(t.EndUtc, tz).DateTime;
                if (Overlaps(startBiz, endBiz, s, e)) return "This time overlaps with Time Off.";
            }

            var blockStart = startBiz.AddMinutes(-service.BufferBefore);
            var blockEnd = endBiz.AddMinutes(service.BufferAfter);

            var appts = _unitOfWork.Appointment
                .GetAll(a => a.BusinessId == businessId && a.CancelledUtc == null && a.Status != AppointmentStatus.Cancelled, includeProperties: "Service")
                .ToList();

            foreach (var a in appts)
            {
                if (excludeAppointmentId.HasValue && a.Id == excludeAppointmentId.Value) continue;

                var s0 = TimeZoneInfo.ConvertTime(a.StartUtc, tz).DateTime;
                var e0 = TimeZoneInfo.ConvertTime(a.EndUtc, tz).DateTime;

                var s = s0.AddMinutes(-(a.Service?.BufferBefore ?? 0));
                var e = e0.AddMinutes((a.Service?.BufferAfter ?? 0));

                if (Overlaps(blockStart, blockEnd, s, e)) return "This time overlaps with an existing appointment.";
            }

            return null;
        }

        private static bool Overlaps(DateTime aStart, DateTime aEnd, DateTime bStart, DateTime bEnd)
            => aStart < bEnd && aEnd > bStart;

        private static bool IsOwnerCreated(Appointment appt)
            => !string.IsNullOrWhiteSpace(appt.ConfirmationToken);

        private bool AutoCompletePastConfirmed(List<Appointment> appts, DateTimeOffset nowUtc)
        {
            bool changed = false;

            foreach (var a in appts)
            {
                if (a.CancelledUtc != null || a.Status == AppointmentStatus.Cancelled) continue;
                if (a.Status == AppointmentStatus.NoShow) continue;
                if (a.Status == AppointmentStatus.Completed) continue;

                if (a.Status == AppointmentStatus.Confirmed && a.EndUtc <= nowUtc)
                {
                    a.Status = AppointmentStatus.Completed;
                    _unitOfWork.Appointment.Update(a);
                    changed = true;
                }
            }

            return changed;
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}