using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SimplyAppoint.DataAccess.Repository.IRepository;
using SimplyAppoint.Models;
using SimplyAppoint.Models.ViewModels;
using System.Diagnostics;

namespace SimplyAppointWeb.Controllers
{
    [Area("Admin")]
    [Authorize]
    public class ServicesController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserManager<IdentityUser> _userManager;

        public ServicesController(IUnitOfWork unitOfWork,
                                  UserManager<IdentityUser> userManager)
        {
            _unitOfWork = unitOfWork;
            _userManager = userManager;
        }

        // -------------------- INDEX --------------------

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Index", "Home");

            var business = _unitOfWork.Business.Get(b => b.OwnerUserId == user.Id);
            if (business == null) return RedirectToAction("Index", "Home");

            var services = _unitOfWork.Service
                .GetAll(s => s.BusinessId == business.Id)
                .OrderBy(s => s.Name)
                .ToList();

            var vm = new ServicesIndexVM();

            vm.Services = services.Select(s => new ServicesIndexVM.ServiceRowVM
            {
                Id = s.Id,
                Name = s.Name,
                Duration = s.DurationMinutes,
                Price = s.Price,
                BufferBefore = s.BufferBefore,
                BufferAfter = s.BufferAfter,
                Active = s.IsActive
            }).ToList();

            vm.Total = services.Count;
            vm.ActiveCount = services.Count(s => s.IsActive);

            if (services.Any())
            {
                vm.AvgDuration = (int)Math.Round(services.Average(s => s.DurationMinutes));
                vm.AvgPrice = Math.Round(services.Average(s => s.Price), 2);

                vm.MinPrice = services.Min(s => s.Price);
                vm.MaxPrice = services.Max(s => s.Price);

                vm.MinDuration = services.Min(s => s.DurationMinutes);
                vm.MaxDuration = services.Max(s => s.DurationMinutes);

                var mostExpensive = services.OrderByDescending(s => s.Price).First();
                vm.MostExpensiveName = mostExpensive.Name;
                vm.MostExpensivePrice = mostExpensive.Price;

                var longest = services.OrderByDescending(s => s.DurationMinutes).First();
                vm.LongestName = longest.Name;
                vm.LongestDuration = longest.DurationMinutes;

                vm.ActivePercentage = vm.Total == 0
                    ? 0
                    : (int)Math.Round((vm.ActiveCount * 100.0) / vm.Total);
            }

            return View(vm);
        }

        // -------------------- UPSERT GET --------------------

        public async Task<IActionResult> Upsert(int? id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction(nameof(Index));

            var business = _unitOfWork.Business.Get(b => b.OwnerUserId == user.Id);
            if (business == null) return RedirectToAction(nameof(Index));

            if (id == null)
            {
                return View(new ServiceUpsertVM());
            }

            var service = _unitOfWork.Service.Get(s =>
                s.Id == id && s.BusinessId == business.Id);

            if (service == null) return NotFound();

            var vm = new ServiceUpsertVM
            {
                Id = service.Id,
                Name = service.Name,
                DurationMinutes = service.DurationMinutes,
                Price = service.Price,
                BufferBefore = service.BufferBefore,
                BufferAfter = service.BufferAfter,
                IsActive = service.IsActive,
                CreatedUtc = service.CreatedUtc
            };

            return View(vm);
        }

        // -------------------- UPSERT POST --------------------

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upsert(ServiceUpsertVM vm)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction(nameof(Index));

            var business = _unitOfWork.Business.Get(b => b.OwnerUserId == user.Id);
            if (business == null) return RedirectToAction(nameof(Index));

            if (!ModelState.IsValid)
                return View(vm);

            bool nameExists = _unitOfWork.Service.GetAll(s =>
                s.BusinessId == business.Id &&
                s.Name == vm.Name &&
                (!vm.Id.HasValue || s.Id != vm.Id.Value)
            ).Any();

            if (nameExists)
            {
                ModelState.AddModelError(nameof(vm.Name), "A service with this name already exists.");
                return View(vm);
            }

            if (vm.Id == null)
            {
                var service = new Service
                {
                    BusinessId = business.Id,
                    Name = vm.Name.Trim(),
                    DurationMinutes = vm.DurationMinutes,
                    Price = vm.Price,
                    BufferBefore = vm.BufferBefore,
                    BufferAfter = vm.BufferAfter,
                    IsActive = vm.IsActive,
                    CreatedUtc = DateTimeOffset.UtcNow
                };

                _unitOfWork.Service.Add(service);
                TempData["success"] = "Service created.";
            }
            else
            {
                var service = _unitOfWork.Service.Get(s =>
                    s.Id == vm.Id && s.BusinessId == business.Id);

                if (service == null) return NotFound();

                service.Name = vm.Name.Trim();
                service.DurationMinutes = vm.DurationMinutes;
                service.Price = vm.Price;
                service.BufferBefore = vm.BufferBefore;
                service.BufferAfter = vm.BufferAfter;
                service.IsActive = vm.IsActive;

                _unitOfWork.Service.Update(service);
                TempData["success"] = "Service updated.";
            }

            _unitOfWork.Save();

            return RedirectToAction(nameof(Index));
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}