using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SimplyAppoint.Models;

namespace SimplyAppointWeb.Controllers
{
    [Area("Admin")]
    public class ServicesController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Upsert()
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
