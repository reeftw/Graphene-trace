using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using System.IO;

namespace AdminDashboard.Controllers
{
    public class AdminController : Controller
    {
        private readonly string clinicianFolder;
        private readonly string patientFolder;

        public AdminController(IWebHostEnvironment env)
        {
            // Ensure folders are inside the actual wwwroot
            clinicianFolder = Path.Combine(env.WebRootPath, "ClinicianData");
            patientFolder = Path.Combine(env.WebRootPath, "PatientDetails");

            // Create folders if they don't exist
            EnsureFolderExists(clinicianFolder);
            EnsureFolderExists(patientFolder);

            // Log paths for debugging
            Console.WriteLine($"Clinician folder path: {clinicianFolder}");
            Console.WriteLine($"Patient folder path: {patientFolder}");
        }

        public IActionResult Index()
        {
            return View("~/Views/Home/admin.cshtml");
        }

        [HttpPost]
        public IActionResult AddClinician(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return BadRequest("Name cannot be empty");

            string id = GenerateRandomId();
            string sanitizedName = SanitizeFileName(name);
            string filePath = Path.Combine(clinicianFolder, $"{id}_{sanitizedName}.txt");

            System.IO.File.WriteAllText(filePath, $"ID: {id}\nName: {name}");

            Console.WriteLine($"Created clinician file: {filePath}"); // Debug log
            return Ok(new { id, name });
        }

        [HttpPost]
        public IActionResult AddPatient(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return BadRequest("Name cannot be empty");

            string id = GenerateRandomId();
            string sanitizedName = SanitizeFileName(name);
            string filePath = Path.Combine(patientFolder, $"{id}_{sanitizedName}.txt");

            System.IO.File.WriteAllText(filePath, $"ID: {id}\nName: {name}");

            Console.WriteLine($"Created patient file: {filePath}"); // Debug log
            return Ok(new { id, name });
        }

        private void EnsureFolderExists(string folder)
        {
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);
        }

        private string GenerateRandomId()
        {
            const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, 8)
              .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        private string SanitizeFileName(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }
    }
}
