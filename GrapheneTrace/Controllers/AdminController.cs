using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GrapheneTrace.Controllers
{
    public class AdminController : Controller
    {
        private readonly IWebHostEnvironment _hostingEnvironment;

        public AdminController(IWebHostEnvironment hostingEnvironment)
        {
            _hostingEnvironment = hostingEnvironment;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View("~/Views/Home/Admin.cshtml");
        }

        // =========================================================
        // 1. READ CLINICIANS (Now ignores deleted files)
        // =========================================================
        [HttpGet]
        [HttpGet]
        public IActionResult GetClinicianList()
        {
            var results = new List<object>(); // Returning objects now
            string folderPath = Path.Combine(_hostingEnvironment.WebRootPath, "clinicianDetails");

            if (Directory.Exists(folderPath))
            {
                var files = Directory.GetFiles(folderPath, "*.txt");

                foreach (var file in files)
                {
                    if (file.EndsWith("-deleted.txt")) continue;

                    try
                    {
                        var lines = System.IO.File.ReadAllLines(file);

                        // Default values
                        string title = "-", first = "-", middle = "-", last = "-", id = "-", created = "-";

                        foreach (var line in lines)
                        {
                            string clean = line.Trim();
                            string lower = clean.ToLower();

                            if (lower.StartsWith("title:")) title = clean.Substring(clean.IndexOf(':') + 1).Trim();
                            else if (lower.StartsWith("first name:")) first = clean.Substring(clean.IndexOf(':') + 1).Trim();
                            else if (lower.StartsWith("middle name:")) middle = clean.Substring(clean.IndexOf(':') + 1).Trim();
                            else if (lower.StartsWith("last name:")) last = clean.Substring(clean.IndexOf(':') + 1).Trim();
                            else if (lower.StartsWith("id number:")) id = clean.Substring(clean.IndexOf(':') + 1).Trim(); // Clinician manual ID
                            else if (lower.StartsWith("gtid:")) id = clean.Substring(clean.IndexOf(':') + 1).Trim(); // Fallback/System ID
                            else if (lower.StartsWith("created at:")) created = clean.Substring(clean.IndexOf(':') + 1).Trim();
                        }

                        // Use filename as the "System ID" for deletion logic
                        string gtid = Path.GetFileNameWithoutExtension(file);

                        if (!string.IsNullOrEmpty(first))
                        {
                            results.Add(new
                            {
                                type = "clinician",
                                gtid = gtid, // The filename ID (for deletion)
                                displayId = id, // The manual ID Number
                                title,
                                first,
                                middle,
                                last,
                                created
                            });
                        }
                    }
                    catch { continue; }
                }
            }
            return Json(results);
        }

        // =========================================================
        // NEW: SOFT DELETE CLINICIAN
        // =========================================================
        [HttpPost]
        public IActionResult DeleteClinician([FromBody] DeleteInput input)
        {
            if (input == null || string.IsNullOrEmpty(input.Gtid))
                return Json(new { success = false, message = "Invalid ID" });

            try
            {
                string folderPath = Path.Combine(_hostingEnvironment.WebRootPath, "clinicianDetails");

                // Construct the current filename
                string currentPath = Path.Combine(folderPath, $"{input.Gtid}.txt");

                // Construct the new "soft deleted" filename
                string newPath = Path.Combine(folderPath, $"{input.Gtid}-deleted.txt");

                if (System.IO.File.Exists(currentPath))
                {
                    // Rename the file (This is the soft delete)
                    System.IO.File.Move(currentPath, newPath);
                    return Json(new { success = true });
                }
                else
                {
                    return Json(new { success = false, message = "Clinician file not found." });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
        // =========================================================
        // 2. ADD CLINICIAN (Generates 8-char Alphanumeric GTID)
        // =========================================================
        [HttpPost]
        public IActionResult AddClinician([FromBody] ClinicianData data)
        {
            if (data == null || string.IsNullOrWhiteSpace(data.LastName))
                return Json(new { success = false, message = "Invalid Data" });

            try
            {
                string gtid = GenerateRandomGtid();
                string folderPath = Path.Combine(_hostingEnvironment.WebRootPath, "clinicianDetails");
                if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

                string filePath = Path.Combine(folderPath, $"{gtid}.txt");

                // UPDATED DATE FORMAT HERE: dd/MM/yyyy HH:mm:ss
                string content = $"GTID: {gtid}\n" +
                                 $"Title: {data.Title}\n" +
                                 $"First Name: {data.FirstName}\n" +
                                 $"Middle Name: {data.MiddleName}\n" +
                                 $"Last Name: {data.LastName}\n" +
                                 $"ID Number: {data.IdNumber}\n" +
                                 $"Created At: {DateTime.Now:dd/MM/yyyy HH:mm:ss}";

                System.IO.File.WriteAllText(filePath, content);

                return Json(new { success = true, gtid = gtid });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Server Error: " + ex.Message });
            }
        }
        // --- Helper to Generate Random String "0cyo9dyp" ---
        private string GenerateRandomGtid()
        {
            const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, 8)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        // =========================================================
        // 5. READ PATIENTS (Now looks for "ID:" instead of GTID)
        // =========================================================
        [HttpGet]
        [HttpGet]
        public IActionResult GetPatientList()
        {
            var results = new List<object>();
            string folderPath = Path.Combine(_hostingEnvironment.WebRootPath, "patientDetails");

            if (Directory.Exists(folderPath))
            {
                var files = Directory.GetFiles(folderPath, "*.txt");

                foreach (var file in files)
                {
                    if (file.EndsWith("-deleted.txt")) continue;

                    try
                    {
                        var lines = System.IO.File.ReadAllLines(file);
                        string title = "-", first = "-", middle = "-", last = "-", id = "-", created = "-";

                        foreach (var line in lines)
                        {
                            string clean = line.Trim();
                            string lower = clean.ToLower();

                            if (lower.StartsWith("title:")) title = clean.Substring(clean.IndexOf(':') + 1).Trim();
                            else if (lower.StartsWith("first name:")) first = clean.Substring(clean.IndexOf(':') + 1).Trim();
                            else if (lower.StartsWith("middle name:")) middle = clean.Substring(clean.IndexOf(':') + 1).Trim();
                            else if (lower.StartsWith("last name:")) last = clean.Substring(clean.IndexOf(':') + 1).Trim();
                            else if (lower.StartsWith("id:")) id = clean.Substring(clean.IndexOf(':') + 1).Trim();
                            else if (lower.StartsWith("created at:")) created = clean.Substring(clean.IndexOf(':') + 1).Trim();
                        }

                        if (!string.IsNullOrEmpty(first))
                        {
                            if (id == "-") id = Path.GetFileNameWithoutExtension(file);

                            results.Add(new
                            {
                                type = "patient",
                                gtid = id, // For patients, the ID is the GTID/Filename
                                displayId = id,
                                title,
                                first,
                                middle,
                                last,
                                created
                            });
                        }
                    }
                    catch { continue; }
                }
            }
            return Json(results);
        }

        // =========================================================
        // 6. ADD PATIENT (Auto-Generates ID, No Manual Input)
        // =========================================================
        [HttpPost]
        public IActionResult AddPatient([FromBody] PatientData data)
        {
            if (data == null || string.IsNullOrWhiteSpace(data.LastName))
                return Json(new { success = false, message = "Invalid Data" });

            try
            {
                string generatedId = GenerateRandomGtid();
                string folderPath = Path.Combine(_hostingEnvironment.WebRootPath, "patientDetails");
                if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

                string filePath = Path.Combine(folderPath, $"{generatedId}.txt");

                // UPDATED DATE FORMAT HERE: dd/MM/yyyy HH:mm:ss
                string content = $"ID: {generatedId}\n" +
                                 $"Title: {data.Title}\n" +
                                 $"First Name: {data.FirstName}\n" +
                                 $"Middle Name: {data.MiddleName}\n" +
                                 $"Last Name: {data.LastName}\n" +
                                 $"Created At: {DateTime.Now:dd/MM/yyyy HH:mm:ss}";

                System.IO.File.WriteAllText(filePath, content);

                string patientDataFolder = Path.Combine(_hostingEnvironment.WebRootPath, "GTLBData", generatedId);
                if (!Directory.Exists(patientDataFolder)) Directory.CreateDirectory(patientDataFolder);

                return Json(new { success = true, gtid = generatedId });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // =========================================================
        // 7. SOFT DELETE PATIENT
        // =========================================================
        [HttpPost]
        public IActionResult DeletePatient([FromBody] DeleteInput input)
        {
            if (input == null || string.IsNullOrEmpty(input.Gtid))
                return Json(new { success = false, message = "Invalid ID" });

            try
            {
                string folderPath = Path.Combine(_hostingEnvironment.WebRootPath, "patientDetails");
                string currentPath = Path.Combine(folderPath, $"{input.Gtid}.txt");
                string newPath = Path.Combine(folderPath, $"{input.Gtid}-deleted.txt");

                if (System.IO.File.Exists(currentPath))
                {
                    System.IO.File.Move(currentPath, newPath);
                    // Note: We do NOT delete the GTLBData folder to preserve medical records safely.
                    return Json(new { success = true });
                }
                else
                {
                    return Json(new { success = false, message = "Patient file not found." });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // =========================================================
        // 8. GET ADMIN REQUESTS (Parses the CSV inbox)
        // =========================================================
        [HttpGet]
        public IActionResult GetAdminRequests()
        {
            var requests = new List<object>();

            // Look in AppData/AdminRequests
            string appData = Path.Combine(_hostingEnvironment.ContentRootPath, "AppData", "AdminRequests");
            string filePath = Path.Combine(appData, "admin_requests.csv");

            if (System.IO.File.Exists(filePath))
            {
                var lines = System.IO.File.ReadAllLines(filePath);

                // Skip Header (row 0)
                foreach (var line in lines.Skip(1))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    // Manual parsing because "Comment" might contain commas inside quotes
                    // Format: Timestamp,UserId,"Comment"

                    try
                    {
                        // 1. Get Timestamp (Everything before first comma)
                        int firstComma = line.IndexOf(',');
                        if (firstComma == -1) continue;
                        string timestamp = line.Substring(0, firstComma);

                        // 2. Get the rest of the string
                        string remainder = line.Substring(firstComma + 1);

                        // 3. Get UserId (Everything before the NEXT comma)
                        int secondComma = remainder.IndexOf(',');
                        if (secondComma == -1) continue;

                        string userId = remainder.Substring(0, secondComma).Trim().Trim('"'); // Remove quotes if present

                        // 4. Get Comment (The rest, remove quotes)
                        string comment = remainder.Substring(secondComma + 1).Trim().Trim('"');

                        // 5. Pretty Date Format
                        if (DateTime.TryParse(timestamp, out DateTime dt))
                        {
                            timestamp = dt.ToString("MMM dd, yyyy HH:mm");
                        }

                        requests.Add(new
                        {
                            timestamp = timestamp,
                            userId = userId,
                            comment = comment
                        });
                    }
                    catch
                    {
                        continue; // Skip malformed lines
                    }
                }
            }

            // Return latest requests first
            return Json(requests.AsEnumerable().Reverse());
        }
    }

    public class ClinicianData
    {
        public string Title { get; set; }
        public string FirstName { get; set; }
        public string MiddleName { get; set; }
        public string LastName { get; set; }
        public string IdNumber { get; set; }
    }

    public class PatientData
    {
        public string Title { get; set; }
        public string FirstName { get; set; }
        public string MiddleName { get; set; }
        public string LastName { get; set; }
    }
    public class DeleteInput
    {
        public string Gtid { get; set; }
    }
}