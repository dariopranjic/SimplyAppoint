using Microsoft.AspNetCore.Mvc;
using SimplyAppoint.DataAccess.Repository.IRepository;
using SimplyAppoint.Models;
using SimplyAppoint.Models.Enums;
using SimplyAppoint.Models.ViewModels;
using SimplyAppointWeb.Extensions;
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

            ModelState.Remove(nameof(model.OwnerUserId));

            if (string.IsNullOrEmpty(data.Business?.Slug) || data.Business.Name != model.Name)
            {
                if (!string.IsNullOrEmpty(model.Name))
                {
                    string baseSlug = model.Name.ToLower().Trim().Replace(" ", "-");
                    baseSlug = System.Text.RegularExpressions.Regex.Replace(baseSlug, @"[^a-z0-9\-]", "");
                    model.Slug = $"{baseSlug}-{Guid.NewGuid().ToString().Substring(0, 4)}";
                    ModelState.Remove(nameof(model.Slug));
                }
            }
            else
            {
                model.Slug = data.Business.Slug;
            }

            if (!ModelState.IsValid) return View(model);

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

            // If clicking Next and the current form is empty/partially filled
            if (submitAction == "Next" && string.IsNullOrWhiteSpace(model.Name))
            {
                if (data.Services.Any())
                {
                    // Clear current errors since we are ignoring the empty form and moving on
                    ModelState.Clear();
                    return RedirectToAction(nameof(WorkingHours));
                }
                else
                {
                    ModelState.AddModelError("Name", "Please add at least one service to continue.");
                    ViewBag.ExistingServices = data.Services;
                    return View(model);
                }
            }

            if (!ModelState.IsValid)
            {
                ViewBag.ExistingServices = data.Services;
                return View(model);
            }

            // Save the current service to session
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
            if (data.Business == null) return RedirectToAction(nameof(Business));

            try
            {
                // 1. Prepare Business
                data.Business.Id = 0;
                data.Business.IsOnboardingComplete = true;

                // 2. Attach Services (1-to-Many)
                if (data.Services != null)
                {
                    data.Services.ForEach(s => s.Id = 0);
                    data.Business.Services = data.Services;
                }

                // 3. Attach Working Hours (1-to-Many)
                if (data.WorkingHours != null)
                {
                    data.WorkingHours.ForEach(w => w.Id = 0);
                    data.Business.WorkingHours = data.WorkingHours;
                }

                // 4. Attach Booking Policy (1-to-1)
                if (data.BookingPolicy != null)
                {
                    data.Business.BookingPolicy = data.BookingPolicy;
                }

                // 5. Save Everything
                _unitOfWork.Business.Add(data.Business);
                _unitOfWork.Save();

                // 6. Cleanup and Redirect
                HttpContext.Session.Remove(SessionKey);

                return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Save failed: " + ex.Message);
                return View("Finish");
            }
        }
    }
}