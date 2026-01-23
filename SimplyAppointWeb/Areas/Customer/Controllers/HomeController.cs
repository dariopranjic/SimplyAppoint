using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SimplyAppoint.Models;
using SimplyAppoint.DataAccess.Repository.IRepository;
using Microsoft.AspNetCore.Identity;

namespace SimplyAppointWeb.Controllers
{
    [Area("Customer")]
    public class HomeController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserManager<IdentityUser> _userManager;

        public HomeController(IUnitOfWork unitOfWork, UserManager<IdentityUser> userManager)
        {
            _unitOfWork = unitOfWork;
            _userManager = userManager;
        }

        /// <summary>
        /// STEP 1: Details Page
        /// Auto-redirects if the user is already authenticated.
        /// </summary>
        public IActionResult Details(string? slug)
        {
            var business = GetBusiness(slug);
            if (business == null)
            {
                return RedirectToOnboarding();
            }

            // If user is logged in, grab their email and skip to Step 2
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                var userEmail = User.FindFirstValue(ClaimTypes.Email);

                return RedirectToAction("Booking", new
                {
                    slug = business.Slug,
                    firstName = "Customer", // Placeholder for default IdentityUser
                    lastName = "",
                    email = userEmail
                });
            }

            return View(business);
        }

        /// <summary>
        /// STEP 2: Booking Page (Selection of Service/Time)
        /// </summary>
        public IActionResult Booking(string slug, string firstName, string lastName, string email)
        {
            var business = GetBusiness(slug);
            if (business == null)
            {
                return RedirectToOnboarding();
            }

            ViewBag.FirstName = firstName;
            ViewBag.LastName = lastName;
            ViewBag.Email = email;

            return View(business);
        }

        /// <summary>
        /// ACTION: This processes the booking and then redirects to Success.
        /// Ensure your form in Booking.cshtml posts to this action.
        /// </summary>
        [HttpPost]
        public IActionResult ConfirmBooking(string slug)
        {
            // Here you would normally save the appointment to the DB
            // e.g., _unitOfWork.Appointment.Add(appointment); _unitOfWork.Save();

            // Redirecting to Success ensures the URL updates correctly
            return RedirectToAction("Success", new { slug = slug });
        }

        /// <summary>
        /// STEP 3: Success Page
        /// </summary>
        public IActionResult Success(string? slug)
        {
            var business = GetBusiness(slug);
            if (business == null)
            {
                return RedirectToOnboarding();
            }

            return View(business);
        }

        #region Helper Methods

        private Business? GetBusiness(string? slug)
        {
            Business? business = null;

            if (!string.IsNullOrEmpty(slug))
            {
                business = _unitOfWork.Business.Get(
                    u => u.Slug == slug,
                    includeProperties: "Services,WorkingHours"
                );
            }

            if (business == null)
            {
                business = _unitOfWork.Business.GetAll(
                    includeProperties: "Services,WorkingHours"
                ).FirstOrDefault();
            }

            return business;
        }

        private IActionResult RedirectToOnboarding()
        {
            return RedirectToAction("Index", "Onboarding", new { area = "Admin" });
        }

        #endregion

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}