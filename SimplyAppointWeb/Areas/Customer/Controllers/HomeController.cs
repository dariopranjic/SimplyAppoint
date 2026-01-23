using System.Diagnostics;
using System.Linq;
using System.Security.Claims;
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
            var service = business?.Services.FirstOrDefault(s => s.Id == serviceId);
            if (business == null || service == null) return RedirectToOnboarding();

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

            DateTime startLocal = DateTime.Parse($"{appointmentDate} {time}");

            var appointment = new Appointment
            {
                BusinessId = business.Id,
                ServiceId = serviceId,
                CustomerName = customer.FullName,
                CustomerEmail = customer.Email,
                StartUtc = startLocal,
                EndUtc = startLocal.AddMinutes(service.DurationMinutes),
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
                TempData["error"] = "Greška kod slanja maila, ali termin je zaprimljen.";
            }

            return RedirectToAction("Success", new { slug = slug });
        }

        [HttpPost]
        public async Task<IActionResult> ResendConfirmation(string slug)
        {
            var business = GetBusiness(slug);
            if (business == null) return RedirectToAction("Index", "Home");

            var appointment = _unitOfWork.Appointment.GetAll()
                .Where(u => u.BusinessId == business.Id && u.Status == AppointmentStatus.Pending)
                .OrderByDescending(u => u.CreatedUtc)
                .FirstOrDefault();

            if (appointment != null)
            {
                try
                {
                    string fName = appointment.CustomerName.Split(' ')[0];
                    await SendVerificationEmail(appointment, business, fName);
                    TempData["success"] = "Novi verifikacijski mail je poslan!";
                }
                catch (Exception)
                {
                    TempData["error"] = "Greška pri ponovnom slanju.";
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
                TempData["success"] = "Termin je već ranije potvrđen!";
                return RedirectToAction("Index", "Home");
            }

            appointment.Status = AppointmentStatus.Confirmed;
            appointment.ConfirmationToken = null;

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

            return View("FinalConfirmation", appointment.Business);
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
            <div style='background-color: #f4f7fa; padding: 50px 20px; font-family: -apple-system, BlinkMacSystemFont, ""Segoe UI"", Roboto, Helvetica, Arial, sans-serif;'>
                <div style='max-width: 500px; margin: 0 auto; background: #ffffff; border-radius: 16px; overflow: hidden; box-shadow: 0 4px 12px rgba(0,0,0,0.08);'>
                    <div style='background: #0d6efd; padding: 30px; text-align: center; color: #ffffff;'>
                        <h2 style='margin: 0; font-size: 24px;'>Potvrdite dolazak</h2>
                    </div>
                    <div style='padding: 40px; text-align: center;'>
                        <p style='font-size: 16px; color: #4b5563; margin-bottom: 25px;'>Pozdrav {firstName}, kliknite na gumb ispod kako biste potvrdili svoj termin u <strong>{business.Name}</strong>.</p>
                        <div style='margin-bottom: 30px; padding: 20px; background: #f8fafc; border-radius: 12px;'>
                            <span style='display: block; font-size: 20px; font-weight: bold; color: #1e293b;'>{appointment.StartUtc.ToString("dd.MM.yyyy.")}</span>
                            <span style='font-size: 18px; color: #64748b;'>u {appointment.StartUtc.ToString("HH:mm")} h</span>
                        </div>
                        <a href='{callbackUrl}' style='display: inline-block; background: #0d6efd; color: #ffffff; padding: 16px 32px; text-decoration: none; border-radius: 12px; font-weight: 600; font-size: 16px;'>POTVRDI TERMIN</a>
                    </div>
                </div>
            </div>";

            await _emailSender.SendEmailAsync(appointment.CustomerEmail, "Akcija potrebna: Potvrdite svoj termin", htmlMessage);
        }

        private async Task SendFinalConfirmationEmail(Appointment appointment)
        {
            string googleUrl = GenerateGoogleCalendarLink(appointment);

            string htmlMessage = $@"
            <div style='background-color: #f4f7fa; padding: 50px 20px; font-family: -apple-system, BlinkMacSystemFont, ""Segoe UI"", Roboto, Helvetica, Arial, sans-serif;'>
                <div style='max-width: 550px; margin: 0 auto; background: #ffffff; border-radius: 16px; overflow: hidden; box-shadow: 0 4px 20px rgba(0,0,0,0.1);'>
                    <div style='background: #198754; padding: 30px; text-align: center; color: #ffffff;'>
                        <h2 style='margin: 0; font-size: 24px;'>Termin je potvrđen! ✅</h2>
                    </div>
                    <div style='padding: 40px;'>
                        <p style='font-size: 16px; color: #334155; margin-bottom: 25px;'>Vaša rezervacija kod <strong>{appointment.Business.Name}</strong> je uspješno verificirana.</p>
                        
                        <div style='border-left: 4px solid #198754; background: #f0fdf4; padding: 20px; border-radius: 0 12px 12px 0; margin-bottom: 30px;'>
                            <table style='width: 100%; font-size: 15px; color: #475569;'>
                                <tr><td style='padding-bottom: 8px;'><strong>Usluga:</strong></td><td style='text-align: right;'>{appointment.Service?.Name}</td></tr>
                                <tr><td style='padding-bottom: 8px;'><strong>Datum:</strong></td><td style='text-align: right;'>{appointment.StartUtc.ToString("dd.MM.yyyy.")}</td></tr>
                                <tr><td style='padding-bottom: 8px;'><strong>Vrijeme:</strong></td><td style='text-align: right;'>{appointment.StartUtc.ToString("HH:mm")} h</td></tr>
                                <tr><td style='border-top: 1px solid #dcfce7; padding-top: 8px;'><strong>Cijena:</strong></td><td style='border-top: 1px solid #dcfce7; padding-top: 8px; text-align: right; font-weight: bold;'>{appointment.Price} €</td></tr>
                            </table>
                        </div>

                        <div style='text-align: center; margin-bottom: 20px;'>
                            <a href='{googleUrl}' style='display: inline-block; padding: 12px 24px; background: #ffffff; color: #198754; border: 2px solid #198754; text-decoration: none; border-radius: 10px; font-weight: bold; font-size: 14px;'>📅 Dodaj u Google Kalendar</a>
                        </div>
                        
                        <p style='font-size: 13px; color: #94a3b8; text-align: center; margin-top: 30px;'>Ovaj mail služi kao vaša službena potvrda. Vidimo se!</p>
                    </div>
                </div>
            </div>";

            await _emailSender.SendEmailAsync(appointment.CustomerEmail, "Potvrda rezervacije - " + appointment.Business.Name, htmlMessage);
        }

        private string GenerateGoogleCalendarLink(Appointment app)
        {
            var start = app.StartUtc.ToString("yyyyMMddTHHmmssZ");
            var end = app.EndUtc.ToString("yyyyMMddTHHmmssZ");
            var details = $"Usluga: {app.Service?.Name}. Cijena: {app.Price} EUR.";
            return $"https://www.google.com/calendar/render?action=TEMPLATE&text={Uri.EscapeDataString("Termin: " + app.Service?.Name)}&dates={start}/{end}&details={Uri.EscapeDataString(details)}&location={Uri.EscapeDataString(app.Business?.Name)}&sf=true&output=xml";
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