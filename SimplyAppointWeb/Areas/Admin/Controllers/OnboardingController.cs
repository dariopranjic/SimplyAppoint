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
            return View();
        }

        public IActionResult Services()
        {
            return View();
        }

        public IActionResult WorkingHours()
        {
            return View();
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
