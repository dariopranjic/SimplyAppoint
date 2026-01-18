using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SimplyAppoint.Models;
using SimplyAppoint.DataAccess.Repository.IRepository;

namespace SimplyAppointWeb.Controllers
{
    [Area("Customer")]
    public class HomeController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        public HomeController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public IActionResult Index(string? slug)
        {
            Business? business = null;

            if (!string.IsNullOrEmpty(slug))
            {
                // Try to find by slug
                business = _unitOfWork.Business.Get(u => u.Slug == slug, includeProperties: "Services,WorkingHours");
            }

            // FALLBACK: If slug is null or not found, just get the first business 
            // This prevents the "Null Reference" error during development.
            if (business == null)
            {
                business = _unitOfWork.Business.GetAll(includeProperties: "Services,WorkingHours").FirstOrDefault();
            }

            // If there are NO businesses in the DB at all, redirect to onboarding
            if (business == null)
            {
                return RedirectToAction("Index", "Onboarding", new { area = "Admin" });
            }

            return View(business);
        }


        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
