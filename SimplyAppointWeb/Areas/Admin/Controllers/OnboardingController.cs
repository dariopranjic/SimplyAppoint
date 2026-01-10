using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SimplyAppoint.Models;

namespace SimplyAppointWeb.Controllers
{
    [Area("Admin")]
    public class OnboardingController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Business()
        {
            return View(new Business());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Business(Business model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            return RedirectToAction(nameof(Services));
        }

        public IActionResult Services()
        {
            return View(new Service());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Services(Service model, string submitAction)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (submitAction == "AddAnother")
            {
                ModelState.Clear();
                var newModel = new Service { BusinessId = model.BusinessId };

                ViewBag.Message = "Service added successfully! You can add another or continue.";

                return View(newModel);
            }

            return RedirectToAction(nameof(WorkingHours));
        }

        public IActionResult WorkingHours()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult WorkingHours(object workingHoursModel) 
        {
            return RedirectToAction(nameof(BookingPolicy));
        }

        public IActionResult BookingPolicy()
        {
            return View();
        }

        public IActionResult Finish()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}