using Microsoft.AspNetCore.Mvc;
using SimplyAppoint.DataAccess.Repository.IRepository;
using SimplyAppoint.Models;
using SimplyAppoint.Models.Enums;
using SimplyAppoint.Models.ViewModels;
using SimplyAppointWeb.Extensions;
using System.Diagnostics;
using System.Security.Claims;

namespace SimplyAppointWeb.Controllers
{
    [Area("Admin")]
    public class OnboardingController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private const string SessionKey = "OnboardingData";

        public OnboardingController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        private OnboardingVM GetSessionData()
        {
            return HttpContext.Session.Get<OnboardingVM>(SessionKey) ?? new OnboardingVM();
        }

        public IActionResult Index()
        {
            HttpContext.Session.Remove(SessionKey);
            return View();
        }

        // STEP 1: BUSINESS DETAILS
        public IActionResult Business()
        {
            var data = GetSessionData();
            var model = data.Business ?? new Business();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Business(Business model)
        {
            var data = GetSessionData();
            model.OwnerUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "Unknown";

            // IMPROVEMENT: Only generate a new slug if the business name changed or if it doesn't exist yet
            if (string.IsNullOrEmpty(data.Business?.Slug) || data.Business.Name != model.Name)
            {
                if (!string.IsNullOrEmpty(model.Name))
                {
                    string baseSlug = model.Name.ToLower().Trim().Replace(" ", "-");
                    baseSlug = System.Text.RegularExpressions.Regex.Replace(baseSlug, @"[^a-z0-9\-]", "");
                    model.Slug = $"{baseSlug}-{Guid.NewGuid().ToString().Substring(0, 4)}";
                }
            }
            else
            {
                model.Slug = data.Business.Slug; // Preserve the existing slug if the name is the same
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            data.Business = model;
            HttpContext.Session.Set(SessionKey, data);

            return RedirectToAction(nameof(Services));
        }

        // STEP 2: SERVICES
        public IActionResult Services()
        {
            var data = GetSessionData();
            ViewBag.ExistingServices = data.Services;
            return View(new Service());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Services(Service model, string submitAction)
        {
            var data = GetSessionData();

            if (submitAction == "Next" && string.IsNullOrEmpty(model.Name))
            {
                if (data.Services.Any())
                {
                    return RedirectToAction(nameof(WorkingHours));
                }
                else
                {
                    ModelState.AddModelError("", "Please add at least one service to continue.");
                }
            }

            if (!ModelState.IsValid)
            {
                ViewBag.ExistingServices = data.Services;
                return View(model);
            }

            data.Services.Add(model);
            HttpContext.Session.Set(SessionKey, data);

            if (submitAction == "AddAnother")
            {
                return RedirectToAction(nameof(Services));
            }

            return RedirectToAction(nameof(WorkingHours));
        }

        // STEP 3: WORKING HOURS
        public IActionResult WorkingHours()
        {
            var data = GetSessionData();

            if (data.WorkingHours == null || !data.WorkingHours.Any())
            {
                data.WorkingHours = new List<WorkingHours>();

                foreach (Weekday day in Enum.GetValues(typeof(Weekday)))
                {
                    data.WorkingHours.Add(new WorkingHours
                    {
                        Weekday = day,
                        OpenTime = new TimeOnly(9, 0),
                        CloseTime = new TimeOnly(17, 0),
                        IsClosed = (day == Weekday.Saturday || day == Weekday.Sunday)
                    });
                }

                HttpContext.Session.Set(SessionKey, data);
            }

            return View(data.WorkingHours);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult WorkingHours(List<WorkingHours> model)
        {
            var data = GetSessionData();
            data.WorkingHours = model;
            HttpContext.Session.Set(SessionKey, data);

            return RedirectToAction(nameof(BookingPolicy));
        }

        // STEP 4: BOOKING POLICY
        public IActionResult BookingPolicy()
        {
            var data = GetSessionData();
            // IMPROVEMENT: Provide sensible defaults if session is empty
            var model = data.BookingPolicy ?? new BookingPolicy
            {
                SlotIntervalMinutes = 30,
                MaxAdvanceDays = 60,
                AdvanceNoticeMinutes = 1440,
                CancellationWindowMinutes = 1440
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult BookingPolicy(BookingPolicy model)
        {
            if (!ModelState.IsValid) return View(model);

            var data = GetSessionData();
            data.BookingPolicy = model;
            HttpContext.Session.Set(SessionKey, data);

            return RedirectToAction(nameof(Finish));
        }

        // STEP 5: FINISH
        public IActionResult Finish()
        {
            return View();
        }

        [HttpPost]
        [ActionName("Finish")]
        [ValidateAntiForgeryToken]
        public IActionResult FinishPost()
        {
            var data = GetSessionData();

            // Guard check: ensure we have data to save
            if (data.Business == null) return RedirectToAction(nameof(Business));

            try
            {
                // 1. Save Business first to generate the ID
                data.Business.IsOnboardingComplete = true;
                data.Business.Id = 0; // SAFETY RESET: Ensure EF treats this as new

                _unitOfWork.Business.Add(data.Business);
                _unitOfWork.Save(); // Database generates the BusinessId here

                int newBusinessId = data.Business.Id;

                // 2. Save Services
                if (data.Services != null && data.Services.Any())
                {
                    foreach (var service in data.Services)
                    {
                        service.Id = 0; // SAFETY RESET
                        service.BusinessId = newBusinessId;
                        _unitOfWork.Service.Add(service);
                    }
                }

                // 3. Save Working Hours
                if (data.WorkingHours != null && data.WorkingHours.Any())
                {
                    foreach (var hours in data.WorkingHours)
                    {
                        hours.Id = 0; // SAFETY RESET
                        hours.BusinessId = newBusinessId;
                        _unitOfWork.WorkingHours.Add(hours);
                    }
                }

                // 4. Save Booking Policy
                if (data.BookingPolicy != null)
                {
                    data.BookingPolicy.BusinessId = newBusinessId;
                    _unitOfWork.BookingPolicy.Add(data.BookingPolicy);
                }

                _unitOfWork.Save();

                // 5. Cleanup session after successful save
                HttpContext.Session.Remove(SessionKey);

                return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
            }
            catch (Exception ex)
            {
                // In a real app, log the exception: _logger.LogError(ex, "Save failed");
                ModelState.AddModelError("", "Something went wrong while saving your setup. Please try again.");
                return View("Finish");
            }
        }
    }
}