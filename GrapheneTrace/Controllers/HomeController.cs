using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GrapheneTrace.Models;
using GrapheneTrace.ViewModels;


namespace GrapheneTrace.Controllers
{
    public class AdminRequestInput
    {
        public string UserId { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;
    }

    public class HomeController : Controller
    {
        private readonly IWebHostEnvironment _hostingEnvironment;
        private readonly ILogger<HomeController> _logger;

        private const string DATA_FOLDER_NAME = "wwwroot/GTLBData";
        private const string COMMENTS_FOLDER_NAME = "wwwroot/GTLBComments";
        private const string ADMIN_REQUESTS_FOLDER_NAME = "AppData/AdminRequests";
        private const string MEDICATIONS_FOLDER_NAME = "GTLBMeds";

        private const int MATRIX_SIZE = 32;
        private const int ALERT_THRESHOLD = 200;
        private const int MIN_CONTACT_PRESSURE = 10;

        public HomeController(IWebHostEnvironment hostingEnvironment, ILogger<HomeController> logger)
        {
            _hostingEnvironment = hostingEnvironment;
            _logger = logger;
        }

        public IActionResult Index() => View();

        public IActionResult Clinician() => View();

        public IActionResult Admin() => View();

        public IActionResult Patient(string patientId = "d13043b3")
        {
            string dataRoot = Path.Combine(_hostingEnvironment.WebRootPath, "GTLBData");
            string commentsRoot = Path.Combine(_hostingEnvironment.WebRootPath, "GTLBComments");

            var model = new PatientHomeViewModel
            {
                PatientName = $"Patient {patientId}",
                UserId = 54321,
                PressureData = new HealthMetric
                {
                    Title = "Pressure Heatmap",
                    GlanceableInfo = "Latest sensor data visualization."
                },
                VisitedDoctors = new List<Clinician>
                {
                    new Clinician { Name = "Dr. Ben Carson", Specialization = "Neurologist" },
                    new Clinician { Name = "Dr. Lisa Cuddy", Specialization = "Endocrinologist" }
                }
            };

            try
            {
                var latestFile = Directory.GetFiles(dataRoot, $"{patientId}_*.csv")
                                          .OrderByDescending(f => f)
                                          .FirstOrDefault();

                if (latestFile != null)
                {
                    var frames = LoadAllFrames(latestFile);

                    if (frames.Count == 0)
                    {
                        _logger.LogWarning("No frames loaded from file {File} for patient {PatientId}", latestFile, patientId);
                    }
                    else
                    {
                        var heatmapData = new HeatmapData
                        {
                            Frames = frames,
                            CurrentFrame = 0,
                            PatientId = patientId,
                            GTLBData = Path.GetFileName(latestFile)
                        };

                        // Compute metrics from the first frame
                        CalculateMetrics(heatmapData);

                        // Append latest comment (if any) to GTLBData string
                        string commentFile = Path.Combine(commentsRoot, $"{patientId}_comments.csv");
                        if (System.IO.File.Exists(commentFile))
                        {
                            var lines = System.IO.File.ReadAllLines(commentFile);
                            if (lines.Length > 1)
                            {
                                var lastLine = lines.Last();
                                var parts = lastLine.Split(',');
                                if (parts.Length >= 3)
                                {
                                    var comment = parts[2].Trim('"');
                                    heatmapData.GTLBData += $" | Comment: {comment}";
                                }
                            }
                        }

                        model.Heatmap = heatmapData;
                    }
                }
                else
                {
                    _logger.LogWarning("No data file found for patient {PatientId}", patientId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading heatmap data for patient {PatientId}", patientId);
            }

            return View(model);
        }

        // --- Patient Metadata (for Clinician Dashboard) ---
        [HttpGet]
        public IActionResult GetPatientFilesMetadata()
        {
            string dataRootPath = Path.Combine(_hostingEnvironment.ContentRootPath, DATA_FOLDER_NAME);
            var patientGroups = new List<PatientGroup>();

            if (!Directory.Exists(dataRootPath))
            {
                _logger.LogWarning("GTLB-Data folder not found at: {Path}", dataRootPath);
                return Json(patientGroups);
            }

            var patientDirectories = Directory.GetDirectories(dataRootPath);

            if (patientDirectories.Length == 0)
            {
                var filesInRoot = Directory.GetFiles(dataRootPath, "*.csv");
                var grouped = filesInRoot.GroupBy(fp =>
                {
                    var name = Path.GetFileNameWithoutExtension(fp);
                    var parts = name.Split('_');
                    return parts[0];
                });

                foreach (var grp in grouped)
                {
                    var group = new PatientGroup { PatientId = grp.Key };
                    foreach (var filePath in grp)
                    {
                        try
                        {
                            var summaryData = ReadAndSummarizeCsv(filePath, grp.Key, Path.GetFileName(filePath));
                            group.Files.Add(summaryData);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error processing file {FilePath}", filePath);
                        }
                    }

                    if (group.Files.Any())
                        patientGroups.Add(group);
                }
            }
            else
            {
                foreach (var patientDir in patientDirectories)
                {
                    string patientId = new DirectoryInfo(patientDir).Name;
                    var group = new PatientGroup { PatientId = patientId };
                    var files = Directory.GetFiles(patientDir, "*.csv");

                    foreach (var filePath in files)
                    {
                        try
                        {
                            var summaryData = ReadAndSummarizeCsv(filePath, patientId, Path.GetFileName(filePath));
                            group.Files.Add(summaryData);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error processing file {FilePath}", filePath);
                        }
                    }

                    if (group.Files.Any())
                        patientGroups.Add(group);
                }
            }

            return Json(patientGroups);
        }

        [HttpGet]
        public IActionResult GetHeatmapPartial(string patientId, string fileName)
        {
            string baseDataPath = Path.Combine(_hostingEnvironment.ContentRootPath, DATA_FOLDER_NAME);
            string patientFolderPath = Path.Combine(baseDataPath, patientId ?? string.Empty);
            string fullPath = !string.IsNullOrEmpty(patientId) && Directory.Exists(patientFolderPath)
                ? Path.Combine(patientFolderPath, fileName)
                : Path.Combine(baseDataPath, fileName);

            try
            {
                if (!System.IO.File.Exists(fullPath))
                {
                    return StatusCode(404, $"Data file not found: {fullPath}");
                }

                var frames = LoadAllFrames(fullPath);

                if (frames.Count == 0)
                {
                    return StatusCode(500, "No frames could be loaded from the specified file.");
                }

                var model = new HeatmapData
                {
                    Frames = frames,
                    CurrentFrame = 0,
                    PatientId = patientId ?? string.Empty,
                    GTLBData = Path.GetFileName(fullPath)
                };

                CalculateMetrics(model);

                ViewData["ShowHeatmapInsights"] = true;
                return PartialView("_HeatmapPartial", model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading partial heatmap for {File}", fullPath);
                return StatusCode(500, $"Error: {ex.Message}");
            }
        }

        // --- NEW: JSON API FOR CALENDAR (load CSV by date) ---
        // date: "yyyy-MM-dd", filenames: "<patientId>_yyyyMMdd.csv"
        [HttpGet]
        public IActionResult GetDataByDate(string patientId, string date)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(patientId) || string.IsNullOrWhiteSpace(date))
                    return BadRequest("patientId and date are required.");

                string dataRoot = Path.Combine(_hostingEnvironment.WebRootPath, "GTLBData");

                string datePart = date.Replace("-", ""); // "2025-10-11" -> "20251011"
                string pattern = $"{patientId}_{datePart}.csv";

                var file = Directory.GetFiles(dataRoot, pattern)
                                    .OrderByDescending(f => f)
                                    .FirstOrDefault();

                if (file == null)
                    return NotFound("No data file for that date.");

                var frames = LoadAllFrames(file);

                if (frames.Count == 0)
                    return StatusCode(500, "No frames found in data file.");

                var heatmap = new HeatmapData
                {
                    Frames = frames,
                    CurrentFrame = 0,
                    PatientId = patientId,
                    GTLBData = Path.GetFileName(file)
                };

                CalculateMetrics(heatmap);

                return Json(new
                {
                    frames = heatmap.Frames,
                    peakPressureIndex = heatmap.PeakPressureIndex,
                    contactAreaPercent = heatmap.ContactAreaPercent,
                    isAlertGenerated = heatmap.IsAlertGenerated,
                    fileName = heatmap.GTLBData
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading data by date for {patientId} {date}", patientId, date);
                return StatusCode(500, "Server error loading dated data.");
            }
        }


        public IActionResult Medications(string patientId = "d13043b3")
        {
            var model = new MedicationViewModel
            {
                PatientId = patientId,

                // STATIC LIST for now — you can later load from CSV or database
                Medications = new List<string>
        {
            "Ibuprofen - Once daily",
            "Hydrocolloid dressings - Twice daily",
            "Sudocrem® - Once nightly "
        }
            };

            return View(model);
        }

        public IActionResult ViewAlerts(string patientId = "d13043b3")
        {
            string dataRoot = Path.Combine(_hostingEnvironment.WebRootPath, "GTLBData");
            var model = new AlertViewModel { PatientId = patientId };

            try
            {
                var latestFile = Directory.GetFiles(dataRoot, $"{patientId}_*.csv")
                                          .OrderByDescending(f => f)
                                          .FirstOrDefault();

                if (latestFile == null)
                {
                    model.Alerts.Add("No data found for this patient.");
                    return View("Alerts", model);
                }

                var frames = LoadAllFrames(latestFile);

                for (int f = 0; f < frames.Count; f++)
                {
                    int maxPressure = 0;

                    foreach (var row in frames[f])
                        foreach (var val in row)
                            if (val > maxPressure)
                                maxPressure = val;

                    if (maxPressure >= 380) // Same alert threshold as your constants
                    {
                        model.Alerts.Add($"⚠️ Alert at Frame {f}: Peak Pressure = {maxPressure} mmHg");
                    }
                }

                if (model.Alerts.Count == 0)
                    model.Alerts.Add("No alerts detected. Pressure remained within safe limits.");
            }
            catch
            {
                model.Alerts.Add("Error loading alert data.");
            }

            return View("Alerts", model);
        }


        public IActionResult SignOut()
        {
            // You are not using ASP.NET Identity, so no real logout is needed.
            // Just redirect to login page.
            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        public IActionResult Search(string searchQuery)
        {
            if (string.IsNullOrWhiteSpace(searchQuery))
                return RedirectToAction("Patient", new { patientId = "d13043b3" });

            searchQuery = searchQuery.Trim().ToLower();

            // ===== SEARCH FOR ALERTS =====
            if (searchQuery.Contains("alert") ||
                searchQuery.Contains("pressure") ||
                searchQuery.Contains("high"))
            {
                return RedirectToAction("ViewAlerts", new { patientId = "d13043b3" });
            }

            // ===== SEARCH FOR MEDICATIONS =====
            if (searchQuery.Contains("med") ||
                searchQuery.Contains("drug") ||
                searchQuery.Contains("tablet") ||
                searchQuery.Contains("prescription"))
            {
                return RedirectToAction("Medications", new { patientId = "d13043b3" });
            }

            // ===== SEARCH FOR FEEDBACK =====
            if (searchQuery.Contains("feedback") ||
                searchQuery.Contains("comment") ||
                searchQuery.Contains("note"))
            {
                // This opens Patient Dashboard AND auto-opens Feedback section
                TempData["OpenFeedback"] = true;
                return RedirectToAction("Patient", new { patientId = "d13043b3" });
            }

            // ===== SEARCH FOR DOCTORS =====
            if (searchQuery.Contains("carson") || searchQuery.Contains("ben"))
            {
                TempData["HighlightDoctor"] = "Ben Carson";
                return RedirectToAction("Patient", new { patientId = "d13043b3" });
            }

            if (searchQuery.Contains("cuddy") || searchQuery.Contains("lisa"))
            {
                TempData["HighlightDoctor"] = "Lisa Cuddy";
                return RedirectToAction("Patient", new { patientId = "d13043b3" });
            }

            // ===== SEARCH FOR DASHBOARD =====
            if (searchQuery.Contains("heat") ||
                searchQuery.Contains("dashboard") ||
                searchQuery.Contains("main"))
            {
                return RedirectToAction("Patient", new { patientId = "d13043b3" });
            }

            // DEFAULT → go to dashboard
            return RedirectToAction("Patient", new { patientId = "d13043b3" });
        }





        // --- MULTI-FRAME CSV LOADER ---
        private List<List<List<int>>> LoadAllFrames(string path)
        {
            var lines = System.IO.File.ReadAllLines(path);
            var frames = new List<List<List<int>>>();

            for (int i = 0; i < lines.Length; i += MATRIX_SIZE)
            {
                if (i + MATRIX_SIZE > lines.Length)
                    break;

                var matrix = new List<List<int>>();

                for (int r = 0; r < MATRIX_SIZE; r++)
                {
                    var row = lines[i + r].Split(',');
                    var rowVals = new List<int>();

                    foreach (var v in row)
                    {
                        if (int.TryParse(v.Trim(), out int val))
                            rowVals.Add(val);
                        else
                            rowVals.Add(0);
                    }

                    while (rowVals.Count < MATRIX_SIZE)
                        rowVals.Add(0);
                    if (rowVals.Count > MATRIX_SIZE)
                        rowVals = rowVals.Take(MATRIX_SIZE).ToList();

                    matrix.Add(rowVals);
                }

                frames.Add(matrix);
            }

            return frames;
        }

        private void CalculateMetrics(HeatmapData model)
        {
            int maxPressure = 0;
            int contactCount = 0;
            const int TOTAL_PIXELS = MATRIX_SIZE * MATRIX_SIZE;

            foreach (var row in model.PressureMatrix)
            {
                foreach (var val in row)
                {
                    maxPressure = Math.Max(maxPressure, val);
                    if (val >= MIN_CONTACT_PRESSURE) contactCount++;
                }
            }

            model.PeakPressureIndex = maxPressure;
            model.ContactAreaPercent = (int)Math.Round((double)contactCount / TOTAL_PIXELS * 100.0);
            model.IsAlertGenerated = maxPressure >= ALERT_THRESHOLD;
        }

        private PatientFile ReadAndSummarizeCsv(string path, string patientId, string fileName)
        {
            const int MATRIX_SIZE = 32;
            const int MIN_CONTACT_PRESSURE = 10;
            const int ALERT_THRESHOLD = 200;
            const int MATRIX_PREVIEW_SIZE = 8;
            const int TOTAL_PIXELS = MATRIX_SIZE * MATRIX_SIZE;

            var lines = System.IO.File.ReadLines(path).Take(MATRIX_SIZE).ToList();

            int maxPressure = 0;
            int contactCount = 0;
            var miniMatrix = new List<List<int>>();

            for (int i = 0; i < lines.Count; i++)
            {
                var row = lines[i].Split(',');
                var miniRow = new List<int>();

                for (int j = 0; j < row.Length && j < MATRIX_SIZE; j++)
                {
                    if (int.TryParse(row[j].Trim(), out int val))
                    {
                        maxPressure = Math.Max(maxPressure, val);
                        if (val >= MIN_CONTACT_PRESSURE) contactCount++;

                        if (i % (MATRIX_SIZE / MATRIX_PREVIEW_SIZE) == 0 &&
                            j % (MATRIX_SIZE / MATRIX_PREVIEW_SIZE) == 0)
                        {
                            miniRow.Add(val);
                        }
                    }
                }

                if (i % (MATRIX_SIZE / MATRIX_PREVIEW_SIZE) == 0)
                {
                    miniMatrix.Add(miniRow.Take(MATRIX_PREVIEW_SIZE).ToList());
                }
            }

            bool isAlert = maxPressure >= ALERT_THRESHOLD;
            float contactAreaPercentFloat = (float)Math.Round((double)contactCount / TOTAL_PIXELS * 100.0);

            return new PatientFile
            {
                FileName = fileName,
                PeakPressure = maxPressure,
                ContactArea = (int)contactAreaPercentFloat,
                IsAlert = isAlert,
                SmallMatrix = miniMatrix
            };
        }

        // --- Comment and Admin Request Endpoints ---

        [HttpPost]
        public IActionResult SaveComment(string patientId, string fileName, string comment)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(patientId) || string.IsNullOrWhiteSpace(fileName) || string.IsNullOrWhiteSpace(comment))
                    return BadRequest("Missing required fields");

                string commentsDir = Path.Combine(_hostingEnvironment.WebRootPath ?? _hostingEnvironment.ContentRootPath, "GTLBComments");
                if (!Directory.Exists(commentsDir)) Directory.CreateDirectory(commentsDir);

                string patientCommentsFile = Path.Combine(commentsDir, $"{patientId}_comments.csv");

                if (!System.IO.File.Exists(patientCommentsFile))
                {
                    System.IO.File.WriteAllText(patientCommentsFile, "Timestamp,FileName,Comment\n", Encoding.UTF8);
                }

                string safeComment = comment.Replace("\"", "\"\"");
                string line = $"{DateTime.UtcNow:o},{fileName},\"{safeComment}\"\n";

                System.IO.File.AppendAllText(patientCommentsFile, line, Encoding.UTF8);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save comment for {Patient} {File}", patientId, fileName);
                return StatusCode(500, "Failed to save comment");
            }
        }

        [HttpGet]
        public IActionResult GetAllComments()
        {
            var results = new List<object>();
            try
            {
                string commentsDir = Path.Combine(_hostingEnvironment.WebRootPath ?? _hostingEnvironment.ContentRootPath, "GTLBComments");
                if (!Directory.Exists(commentsDir)) return Json(results);

                var files = Directory.GetFiles(commentsDir, "*_comments.csv");
                foreach (var f in files)
                {
                    string patientId = Path.GetFileName(f).Split('_')[0];
                    var lines = System.IO.File.ReadAllLines(f);
                    if (lines.Length <= 1) continue;

                    foreach (var raw in lines.Skip(1))
                    {
                        if (string.IsNullOrWhiteSpace(raw)) continue;

                        int idx1 = raw.IndexOf(',');
                        if (idx1 < 0) continue;
                        int idx2 = raw.IndexOf(',', idx1 + 1);
                        if (idx2 < 0) continue;

                        string ts = raw.Substring(0, idx1);
                        string fileName = raw.Substring(idx1 + 1, idx2 - idx1 - 1);
                        string commentField = raw.Substring(idx2 + 1).Trim();
                        if (commentField.StartsWith("\"") && commentField.EndsWith("\""))
                        {
                            commentField = commentField.Substring(1, commentField.Length - 2).Replace("\"\"", "\"");
                        }

                        DateTime.TryParse(ts, out DateTime when);

                        results.Add(new
                        {
                            timestamp = when.ToUniversalTime(),
                            patientId = patientId,
                            fileName = fileName,
                            comment = commentField
                        });
                    }
                }

                var ordered = results.OrderByDescending(r => ((DateTime)((dynamic)r).timestamp)).ToList();
                return Json(ordered);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading comments");
                return StatusCode(500, "Error reading comments");
            }
        }

        [HttpPost]
        public IActionResult SubmitAdminRequest([FromBody] AdminRequestInput input)
        {
            try
            {
                if (input == null || string.IsNullOrWhiteSpace(input.UserId) || string.IsNullOrWhiteSpace(input.Comment))
                    return BadRequest(new { message = "UserId and Comment are required" });

                string adminDir = Path.Combine(_hostingEnvironment.ContentRootPath ?? _hostingEnvironment.WebRootPath, ADMIN_REQUESTS_FOLDER_NAME);
                if (!Directory.Exists(adminDir)) Directory.CreateDirectory(adminDir);

                string adminFile = Path.Combine(adminDir, "admin_requests.csv");
                if (!System.IO.File.Exists(adminFile))
                {
                    System.IO.File.WriteAllText(adminFile, "Timestamp,UserId,Comment\n", Encoding.UTF8);
                }

                string safe = input.Comment.Replace("\"", "\"\"");
                string line = $"{DateTime.UtcNow:o},{input.UserId},\"{safe}\"\n";
                System.IO.File.AppendAllText(adminFile, line, Encoding.UTF8);

                return Ok(new { message = "Submitted" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to submit admin request");
                return StatusCode(500, new { message = "Failed to submit request" });
            }
        }

        [HttpPost]
        public IActionResult SaveMedication(string patientId, string medicationText, string clinicianId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(patientId) || string.IsNullOrWhiteSpace(medicationText))
                    return BadRequest("Patient ID and medication text are required");

                string medsDir = Path.Combine(_hostingEnvironment.WebRootPath ?? _hostingEnvironment.ContentRootPath, MEDICATIONS_FOLDER_NAME);
                if (!Directory.Exists(medsDir)) Directory.CreateDirectory(medsDir);

                string medsFile = Path.Combine(medsDir, $"{patientId}_meds.csv");
                if (!System.IO.File.Exists(medsFile))
                {
                    System.IO.File.WriteAllText(medsFile, "Timestamp,ClinicianId,PatientId,Medications\n", Encoding.UTF8);
                }

                string safeText = medicationText.Replace("\"", "\"\"");
                string safeClinician = string.IsNullOrWhiteSpace(clinicianId) ? "unknown" : clinicianId;
                string line = $"{DateTime.UtcNow:o},{safeClinician},{patientId},\"{safeText}\"\n";

                System.IO.File.AppendAllText(medsFile, line, Encoding.UTF8);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save medication for {Patient}", patientId);
                return StatusCode(500, "Failed to save medication");
            }
        }
    }
}
