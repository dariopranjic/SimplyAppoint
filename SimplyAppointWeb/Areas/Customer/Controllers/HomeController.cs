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
                    firstName = "Customer",
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

            // Slanje ulijepšanog maila
            await SendBookingEmail(appointment, business, firstName, false);

            return RedirectToAction("Success", new { slug = slug });
        }

        [HttpPost]
        public async Task<IActionResult> ResendConfirmation(string slug)
        {
            var business = GetBusiness(slug);
            if (business == null) return NotFound();

            var appointment = _unitOfWork.Appointment.GetAll()
                .Where(u => u.BusinessId == business.Id && u.Status == AppointmentStatus.Pending)
                .OrderByDescending(u => u.CreatedUtc)
                .FirstOrDefault();

            if (appointment != null)
            {
                try
                {
                    await SendBookingEmail(appointment, business, appointment.CustomerName.Split(' ')[0], true);
                    TempData["success"] = "Confirmation email resent!";
                }
                catch (Exception ex)
                {
                    TempData["error"] = "Greška pri slanju: " + ex.Message;
                }
            }

            return RedirectToAction("Success", new { slug = slug });
        }

        [HttpGet]
        public IActionResult ConfirmEmail(string token)
        {
            if (string.IsNullOrEmpty(token)) return RedirectToAction("Index", "Home");

            var appointment = _unitOfWork.Appointment.Get(u => u.ConfirmationToken == token);
            if (appointment == null) return View("Error");

            appointment.Status = AppointmentStatus.Confirmed;
            appointment.ConfirmationToken = null;

            _unitOfWork.Appointment.Update(appointment);
            _unitOfWork.Save();

            var business = _unitOfWork.Business.Get(u => u.Id == appointment.BusinessId);
            return View("FinalConfirmation", business);
        }

        public IActionResult Success(string? slug)
        {
            var business = GetBusiness(slug);
            if (business == null) return RedirectToOnboarding();
            return View(business);
        }

        #region Helpers

        private async Task SendBookingEmail(Appointment appointment, Business business, string firstName, bool isResend)
        {
            var callbackUrl = Url.Action("ConfirmEmail", "Home",
                new { token = appointment.ConfirmationToken }, protocol: Request.Scheme);

            string subject = isResend ? "Ponovno slanje: Potvrdite svoj termin" : "Potvrdite svoj termin - " + business.Name;

            string htmlMessage = $@"
            <div style='font-family: ""Segoe UI"", Tahoma, Geneva, Verdana, sans-serif; max-width: 600px; margin: 0 auto; border: 1px solid #e1e4e8; border-radius: 12px; overflow: hidden; box-shadow: 0 4px 12px rgba(0,0,0,0.1);'>
                <div style='background-color: #007bff; padding: 30px; text-align: center; color: white;'>
                    <h1 style='margin: 0; font-size: 24px;'>Potvrda Rezervacije</h1>
                </div>
                <div style='padding: 30px; background-color: #ffffff; color: #333;'>
                    <p style='font-size: 18px;'>Pozdrav <strong>{firstName}</strong>,</p>
                    <p style='line-height: 1.6;'>Hvala vam na rezervaciji kod <strong>{business.Name}</strong>. Kako bismo osigurali vaš termin, molimo vas da potvrdite rezervaciju klikom na gumb ispod:</p>
                    
                    <div style='text-align: center; margin: 40px 0;'>
                        <a href='{callbackUrl}' style='background-color: #28a745; color: white; padding: 16px 32px; text-decoration: none; border-radius: 8px; font-weight: bold; font-size: 18px; display: inline-block; box-shadow: 0 2px 5px rgba(0,0,0,0.2);'>POTVRDI MOJ TERMIN</a>
                    </div>
                    
                    <div style='background-color: #f8f9fa; padding: 20px; border-radius: 8px; border-left: 4px solid #007bff;'>
                        <p style='margin: 5px 0;'><strong>Usluga:</strong> {appointment.Price} €</p>
                        <p style='margin: 5px 0;'><strong>Vrijeme:</strong> {appointment.StartUtc.ToString("dd.MM.yyyy. u HH:mm")} h</p>
                    </div>

                    <p style='font-size: 13px; color: #6c757d; margin-top: 30px; text-align: center;'>
                        Ako gumb ne radi, kopirajte ovaj link u preglednik:<br>
                        <a href='{callbackUrl}' style='color: #007bff;'>{callbackUrl}</a>
                    </p>
                </div>
                <div style='background-color: #f1f3f5; padding: 20px; text-align: center; color: #6c757d; font-size: 12px;'>
                    &copy; {DateTime.Now.Year} SimplyAppoint. Sva prava pridržana.
                </div>
            </div>";

            await _emailSender.SendEmailAsync(appointment.CustomerEmail, subject, htmlMessage);
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
        public IActionResult Error() => View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}