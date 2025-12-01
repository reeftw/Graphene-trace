using Microsoft.AspNetCore.Mvc;
using GrapheneTrace.ViewModels;

namespace GrapheneTrace.Controllers
{
    public class AccountController : Controller
    {
        [HttpGet]
        public IActionResult Manage(string userId, string email, string phone, string medical)
        {
            var model = new EditAccountViewModel
            {
                UserId = userId,
                Email = email,
                PhoneNumber = phone,
                MedicalHistory = medical
            };

            return View(model);
        }

        [HttpPost]
        public IActionResult Save(EditAccountViewModel model)
        {
            if (!ModelState.IsValid)
                return View("Manage", model);

            TempData["Message"] = "Account details updated successfully!";

            return RedirectToAction("Patient", "Home", new { patientId = model.UserId });
        }
    }
}
