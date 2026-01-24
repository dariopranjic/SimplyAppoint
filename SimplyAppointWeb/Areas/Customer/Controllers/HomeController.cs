using System.Diagnostics;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using SimplyAppoint.Models;
using SimplyAppoint.DataAccess.Repository.IRepository;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using SimplyAppoint.Models.Enums;

namespace SimplyAppointWeb.Controllers
{
    [Area("Customer")]
    public class HomeController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IEmailSender _emailSender;

        public HomeController(
            IUnitOfWork unitOfWork,
            UserManager<IdentityUser> userManager,
            IEmailSender emailSender)
        {
            _unitOfWork = unitOfWork;
            _userManager = userManager;
            _emailSender = emailSender;
        }

        public IActionResult Index()
        {
            var businesses = _unitOfWork.Business.GetAll(includeProperties: "Services");
            return View(businesses);
        }

        [HttpGet]
        public IActionResult GetAvailableSlots(int serviceId, DateTime date)
        {
            var service = _unitOfWork.Service.Get(u => u.Id == serviceId);
            if (service == null) return Json(new List<string>());

            var business = _unitOfWork.Business.Get(u => u.Id == service.BusinessId, includeProperties: "WorkingHours");
            var policy = _unitOfWork.BookingPolicy.Get(u => u.BusinessId == service.BusinessId);

            if (business == null || policy == null) return Json(new List<string>());

            int dayNum = (int)date.DayOfWeek;
            if (dayNum == 0) dayNum = 7;

            var workingDay = business.WorkingHours.FirstOrDefault(wh => (int)wh.Weekday == dayNum && !wh.IsClosed);

            if (workingDay == null || !workingDay.OpenTime.HasValue || !workingDay.CloseTime.HasValue)
                return Json(new List<string>());

            var existingBookings = _unitOfWork.Appointment.GetAll(u =>
                u.BusinessId == business.Id &&
                u.StartUtc.Date == date.Date &&
                u.Status != AppointmentStatus.Cancelled);

            var availableSlots = new List<string>();

            DateTime currentSlotStart = date.Date.Add(workingDay.OpenTime.Value.ToTimeSpan());
            DateTime dayEnd = date.Date.Add(workingDay.CloseTime.Value.ToTimeSpan());
            int interval = policy.SlotIntervalMinutes;

            while (currentSlotStart.AddMinutes(service.DurationMinutes) <= dayEnd)
            {
                DateTime currentSlotEnd = currentSlotStart.AddMinutes(service.DurationMinutes);

                bool isOccupied = existingBookings.Any(b =>
                    currentSlotStart < b.StartUtc.AddMinutes(b.DurationMinutes + service.BufferAfter) &&
                    currentSlotEnd > b.StartUtc.AddMinutes(-service.BufferBefore));

                bool isPast = currentSlotStart < DateTime.Now;
                bool satisfiesNotice = currentSlotStart >= DateTime.Now.AddMinutes(policy.AdvanceNoticeMinutes);

                if (!isOccupied && !isPast && satisfiesNotice)
                {
                    availableSlots.Add(currentSlotStart.ToString("HH:mm"));
                }

                currentSlotStart = currentSlotStart.AddMinutes(interval);
            }

            return Json(availableSlots);
        }

        public IActionResult Details(string? slug)
        {
            var business = GetBusiness(slug);
            if (business == null) return RedirectToOnboarding();

            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                var userEmail = User.FindFirstValue(ClaimTypes.Email);
                return RedirectToAction("Booking", new
                {
                    slug = business.Slug,
                    firstName = "",
                    lastName = "",
                    email = userEmail
                });
            }

            return View(business);
        }

        public IActionResult Booking(string slug, string firstName, string lastName, string email)
        {
            var business = GetBusiness(slug);
            if (business == null) return RedirectToOnboarding();

            ViewBag.FirstName = firstName;
            ViewBag.LastName = lastName;
            ViewBag.Email = email;

            return View(business);
        }

        [HttpPost]
        public async Task<IActionResult> ConfirmBooking(string slug, int serviceId, string appointmentDate, string time, string firstName, string lastName, string email)
        {
            var business = GetBusiness(slug);
            var service = _unitOfWork.Service.Get(u => u.Id == serviceId);

            if (business == null || service == null) return RedirectToOnboarding();

            DateTime startLocal = DateTime.Parse($"{appointmentDate} {time}");
            DateTime startUtc = startLocal.ToUniversalTime();

            var existingAppointment = _unitOfWork.Appointment.Get(u =>
                u.BusinessId == business.Id &&
                u.CustomerEmail == email &&
                u.StartUtc == startUtc &&
                u.Status != AppointmentStatus.Cancelled);

            if (existingAppointment != null)
            {
                return RedirectToAction("Success", new { slug = slug });
            }

            BusinessCustomer? customer = null;
            string? currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!string.IsNullOrEmpty(currentUserId))
            {
                customer = _unitOfWork.BusinessCustomer.Get(u => u.UserId == currentUserId && u.BusinessId == business.Id);
            }

            if (customer == null)
            {
                customer = _unitOfWork.BusinessCustomer.Get(u => u.Email.ToLower() == email.ToLower() && u.BusinessId == business.Id);
            }

            if (customer == null)
            {
                customer = new BusinessCustomer
                {
                    BusinessId = business.Id,
                    UserId = currentUserId,
                    FullName = $"{firstName} {lastName}".Trim(),
                    Email = email,
                    CreatedUtc = DateTimeOffset.UtcNow,
                    IsActive = true
                };
                _unitOfWork.BusinessCustomer.Add(customer);
                _unitOfWork.Save();
            }

            var appointment = new Appointment
            {
                BusinessId = business.Id,
                ServiceId = serviceId,
                CustomerName = customer.FullName,
                CustomerEmail = customer.Email,
                StartUtc = startUtc,
                EndUtc = startUtc.AddMinutes(service.DurationMinutes),
                Price = service.Price,
                DurationMinutes = service.DurationMinutes,
                Status = AppointmentStatus.Pending,
                ConfirmationToken = Guid.NewGuid().ToString(),
                CreatedUtc = DateTimeOffset.UtcNow
            };

            _unitOfWork.Appointment.Add(appointment);
            _unitOfWork.Save();

            try
            {
                await SendVerificationEmail(appointment, business, firstName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Email Error: {ex.Message}");
                TempData["error"] = "Appointment saved, but email could not be sent.";
            }

            return RedirectToAction("Success", new { slug = slug });
        }

        [HttpPost]
        public async Task<IActionResult> ResendConfirmation(string slug)
        {
            var business = GetBusiness(slug);
            if (business == null) return RedirectToAction("Index", "Home");

            var appointment = _unitOfWork.Appointment.GetAll(u => u.BusinessId == business.Id && u.Status == AppointmentStatus.Pending)
                .OrderByDescending(u => u.CreatedUtc)
                .FirstOrDefault();

            if (appointment != null)
            {
                try
                {
                    string fName = appointment.CustomerName.Split(' ')[0];
                    await SendVerificationEmail(appointment, business, fName);
                    TempData["success"] = "Verification email resent successfully!";
                }
                catch (Exception)
                {
                    TempData["error"] = "Error resending email.";
                }
            }
            return RedirectToAction("Success", new { slug = slug });
        }

        [HttpGet]
        public async Task<IActionResult> ConfirmEmail(string token)
        {
            if (string.IsNullOrEmpty(token)) return RedirectToAction("Index", "Home");

            var appointment = _unitOfWork.Appointment.Get(u => u.ConfirmationToken == token, includeProperties: "Business,Service");

            if (appointment == null)
            {
                return View("AlreadyConfirmed");
            }

            if (appointment.Status == AppointmentStatus.Pending)
            {
                appointment.Status = AppointmentStatus.Confirmed;
                _unitOfWork.Appointment.Update(appointment);
                _unitOfWork.Save();

                try
                {
                    await SendFinalConfirmationEmail(appointment);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Final Email Error: {ex.Message}");
                }
            }

            return View("FinalConfirmation", appointment.Business);
        }

        [HttpGet]
        public IActionResult CancelAppointment(string token)
        {
            if (string.IsNullOrEmpty(token)) return RedirectToAction("Index");

            var appointment = _unitOfWork.Appointment.Get(u => u.ConfirmationToken == token, includeProperties: "Business");

            if (appointment == null || appointment.Status == AppointmentStatus.Cancelled)
            {
                return View("AppointmentNotFound");
            }

            appointment.Status = AppointmentStatus.Cancelled;
            _unitOfWork.Appointment.Update(appointment);
            _unitOfWork.Save();

            return View("CancelledSuccessfully", appointment.Business);
        }

        [HttpGet]
        public IActionResult DownloadIcs(int appointmentId)
        {
            var app = _unitOfWork.Appointment.Get(u => u.Id == appointmentId, includeProperties: "Business,Service");
            if (app == null) return NotFound();

            var start = app.StartUtc.ToString("yyyyMMddTHHmmssZ");
            var end = app.EndUtc.ToString("yyyyMMddTHHmmssZ");
            string summary = "Booking: " + (app.Service?.Name ?? "Appointment");
            string location = app.Business?.Name ?? "Business";

            var icsContent = new StringBuilder();
            icsContent.Append("BEGIN:VCALENDAR\r\n");
            icsContent.Append("VERSION:2.0\r\n");
            icsContent.Append("PRODID:-//SimplyAppoint//NONSGML v1.0//EN\r\n");
            icsContent.Append("METHOD:PUBLISH\r\n");
            icsContent.Append("BEGIN:VEVENT\r\n");
            icsContent.Append($"UID:simplyappoint-{app.Id}\r\n");
            icsContent.Append($"DTSTAMP:{DateTime.UtcNow:yyyyMMddTHHmmssZ}\r\n");
            icsContent.Append($"DTSTART:{start}\r\n");
            icsContent.Append($"DTEND:{end}\r\n");
            icsContent.Append($"SUMMARY:{summary}\r\n");
            icsContent.Append($"LOCATION:{location}\r\n");
            icsContent.Append("END:VEVENT\r\n");
            icsContent.Append("END:VCALENDAR");

            var bytes = Encoding.UTF8.GetBytes(icsContent.ToString());

            Response.Headers.Add("Content-Disposition", "inline; filename=appointment.ics");
            return File(bytes, "text/calendar");
        }

        public IActionResult Success(string? slug)
        {
            var business = GetBusiness(slug);
            if (business == null) return RedirectToOnboarding();
            return View(business);
        }

        #region Helpers

        private async Task SendVerificationEmail(Appointment appointment, Business business, string firstName)
        {
            var callbackUrl = Url.Action("ConfirmEmail", "Home", new { token = appointment.ConfirmationToken }, protocol: Request.Scheme);

            string htmlMessage = $@"
            <div style='background-color: #f4f7fa; padding: 50px 20px; font-family: sans-serif;'>
                <div style='max-width: 500px; margin: 0 auto; background: #ffffff; border-radius: 16px; overflow: hidden; box-shadow: 0 4px 12px rgba(0,0,0,0.08);'>
                    <div style='background: #0d6efd; padding: 30px; text-align: center; color: #ffffff;'>
                        <h2 style='margin: 0;'>Confirm your Appointment</h2>
                    </div>
                    <div style='padding: 40px; text-align: center;'>
                        <p>Hello {firstName}, please confirm your booking at <strong>{business.Name}</strong>.</p>
                        <div style='margin-bottom: 30px; padding: 20px; background: #f8fafc; border-radius: 12px;'>
                            <span style='display: block; font-size: 20px; font-weight: bold;'>{appointment.StartUtc.ToLocalTime().ToString("dd.MM.yyyy")}</span>
                            <span>at {appointment.StartUtc.ToLocalTime().ToString("HH:mm")}</span>
                        </div>
                        <a href='{callbackUrl}' style='display: inline-block; background: #0d6efd; color: #ffffff; padding: 16px 32px; text-decoration: none; border-radius: 12px; font-weight: 600;'>CONFIRM BOOKING</a>
                    </div>
                </div>
            </div>";

            await _emailSender.SendEmailAsync(appointment.CustomerEmail, "Action Required: Confirm your appointment", htmlMessage);
        }

        private async Task SendFinalConfirmationEmail(Appointment appointment)
        {
            string googleUrl = GenerateGoogleCalendarLink(appointment);
            string appleUrl = Url.Action("DownloadIcs", "Home", new { appointmentId = appointment.Id }, protocol: Request.Scheme) ?? "";
            string cancelUrl = Url.Action("CancelAppointment", "Home", new { token = appointment.ConfirmationToken }, protocol: Request.Scheme) ?? "";

            string businessName = appointment.Business?.Name ?? "Business";

            string htmlMessage = $@"
            <div style='background-color: #f4f7fa; padding: 50px 20px; font-family: sans-serif;'>
                <div style='max-width: 550px; margin: 0 auto; background: #ffffff; border-radius: 16px; overflow: hidden; box-shadow: 0 4px 20px rgba(0,0,0,0.1);'>
                    <div style='background: #198754; padding: 30px; text-align: center; color: #ffffff;'>
                        <h2 style='margin: 0;'>Appointment Confirmed!</h2>
                    </div>
                    <div style='padding: 40px;'>
                        <p>Your booking at <strong>{businessName}</strong> has been successfully verified.</p>
                        <div style='border-left: 4px solid #198754; background: #f0fdf4; padding: 20px; border-radius: 0 12px 12px 0; margin-bottom: 30px;'>
                            <table style='width: 100%; font-size: 14px;'>
                                <tr><td><strong>Service:</strong></td><td style='text-align: right;'>{appointment.Service?.Name}</td></tr>
                                <tr><td><strong>Date:</strong></td><td style='text-align: right;'>{appointment.StartUtc.ToLocalTime().ToString("dd.MM.yyyy")}</td></tr>
                                <tr><td><strong>Time:</strong></td><td style='text-align: right;'>{appointment.StartUtc.ToLocalTime().ToString("HH:mm")}</td></tr>
                                <tr><td><strong>Price:</strong></td><td style='text-align: right; font-weight: bold;'>{appointment.Price} €</td></tr>
                            </table>
                        </div>
                        <div style='text-align: center;'>
                            <p style='font-weight: bold; color: #666; margin-bottom: 15px;'>Add to your calendar:</p>
                            <a href='{googleUrl}' target='_blank' style='display: inline-block; padding: 12px 25px; margin: 5px; border: 1px solid #db4437; text-decoration: none; border-radius: 8px; font-weight: bold; color: #db4437; background: #ffffff;'>Google</a>
                            <a href='{appleUrl}' style='display: inline-block; padding: 12px 25px; margin: 5px; border: 1px solid #000000; text-decoration: none; border-radius: 8px; font-weight: bold; color: #000000; background: #ffffff;'>Apple / Outlook</a>
                        </div>
                        <div style='text-align: center; margin-top: 30px; border-top: 1px solid #eee; padding-top: 20px;'>
                            <p style='font-size: 12px; color: #999; margin-bottom: 10px;'>Need to change plans?</p>
                            <a href='{cancelUrl}' style='color: #dc3545; text-decoration: underline; font-size: 13px;'>Cancel Appointment</a>
                        </div>
                    </div>
                </div>
            </div>";

            await _emailSender.SendEmailAsync(appointment.CustomerEmail, $"Confirmed: {businessName}", htmlMessage);
        }

        private string GenerateGoogleCalendarLink(Appointment app)
        {
            var start = app.StartUtc.ToString("yyyyMMddTHHmmssZ");
            var end = app.EndUtc.ToString("yyyyMMddTHHmmssZ");

            string serviceName = app.Service?.Name ?? "Appointment";
            string businessName = app.Business?.Name ?? "Business";
            var details = $"Service: {serviceName}. Price: {app.Price} EUR.";

            return $"https://www.google.com/calendar/render?action=TEMPLATE" +
                   $"&text={Uri.EscapeDataString("Booking: " + serviceName)}" +
                   $"&dates={start}/{end}" +
                   $"&details={Uri.EscapeDataString(details)}" +
                   $"&location={Uri.EscapeDataString(businessName)}&sf=true&output=xml";
        }

        private Business? GetBusiness(string? slug)
        {
            if (string.IsNullOrEmpty(slug))
            {
                return _unitOfWork.Business.GetAll(includeProperties: "Services,WorkingHours").FirstOrDefault();
            }
            return _unitOfWork.Business.Get(u => u.Slug == slug, includeProperties: "Services,WorkingHours");
        }

        private IActionResult RedirectToOnboarding() => RedirectToAction("Index", "Onboarding", new { area = "Admin" });
        #endregion

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}