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
                    int[,] matrix = LoadSingleMatrix(latestFile);

                    var matrixAsList = new List<List<int>>();
                    for (int i = 0; i < MATRIX_SIZE; i++)
                    {
                        var row = new List<int>();
                        for (int j = 0; j < MATRIX_SIZE; j++)
                            row.Add(matrix[i, j]);
                        matrixAsList.Add(row);
                    }

                    var heatmapData = new HeatmapData
                    {
                        PressureMatrix = matrixAsList,
                        PatientId = patientId,
                        GTLBData = Path.GetFileName(latestFile),
                        TotalMatrices = 1,
                        MatrixIndex = 0
                    };

                    CalculateMetrics(heatmapData);

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
                int[,] requestedMatrix = LoadSingleMatrix(fullPath);
                var matrixAsList = new List<List<int>>();

                for (int i = 0; i < MATRIX_SIZE; i++)
                {
                    matrixAsList.Add(new List<int>());
                    for (int j = 0; j < MATRIX_SIZE; j++)
                        matrixAsList[i].Add(requestedMatrix[i, j]);
                }

                var model = new HeatmapData { PressureMatrix = matrixAsList, TotalMatrices = 1 };
                CalculateMetrics(model);

                return PartialView("_HeatmapPartial", model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading partial heatmap for {File}", fullPath);
                return StatusCode(500, $"Error: {ex.Message}");
            }
        }



        private int[,] LoadSingleMatrix(string path)
        {
            if (!System.IO.File.Exists(path))
                throw new FileNotFoundException("The specified data file was not found.", path);

            var lines = System.IO.File.ReadLines(path).Take(MATRIX_SIZE).ToArray();
            int[,] mat = new int[MATRIX_SIZE, MATRIX_SIZE];

            for (int i = 0; i < lines.Length; i++)
            {
                var row = lines[i].Split(',');
                for (int j = 0; j < MATRIX_SIZE; j++)
                {
                    mat[i, j] = (row.Length > j && int.TryParse(row[j].Trim(), out int val)) ? val : 0;
                }
            }
            return mat;
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

                        // Build a smaller preview matrix (8×8)
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

                // Ensure header exists
                if (!System.IO.File.Exists(patientCommentsFile))
                {
                    System.IO.File.WriteAllText(patientCommentsFile, "Timestamp,FileName,Comment\n", Encoding.UTF8);
                }

                // Escape double-quotes in comment and wrap in quotes
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
                    if (lines.Length <= 1) continue; // no data

                    // skip header
                    foreach (var raw in lines.Skip(1))
                    {
                        if (string.IsNullOrWhiteSpace(raw)) continue;
                        // naive parse: timestamp,fileName,comment (comment may contain commas)
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

                        results.Add(new {
                            timestamp = when.ToUniversalTime(),
                            patientId = patientId,
                            fileName = fileName,
                            comment = commentField
                        });
                    }
                }

                // newest first
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

    }
}
