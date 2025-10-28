using Microsoft.AspNetCore.Mvc;
using GrapheneTrace.Models;
using GrapheneTrace.ViewModels;
using System.Text;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Microsoft.AspNetCore.Hosting;
using System;
using Microsoft.Extensions.Logging;

namespace GrapheneTrace.Controllers
{
    // NOTE: Ensure your GrapheneTrace.Models namespace is correct for all models used.
    public class HomeController : Controller
    {
        private readonly IWebHostEnvironment _hostingEnvironment;
        private readonly ILogger<HomeController> _logger;

        private const string DATA_FOLDER_NAME = "wwwroot/GTLBData";
        private const string COMMENTS_FOLDER_NAME = "wwwroot/GTLBComments";
        private const int MATRIX_SIZE = 32;
        private const int ALERT_THRESHOLD = 200;
        private const int MIN_CONTACT_PRESSURE = 10;

        public HomeController(IWebHostEnvironment hostingEnvironment, ILogger<HomeController> logger)
        {
            _hostingEnvironment = hostingEnvironment;
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Clinician()
        {
            return View();
        }

        public IActionResult Patient()
        {
            var model = new PatientHomeViewModel();
            return View(model);
        }

        public IActionResult Admin()
        {
            return View();
        }

        // --- Action to get Patient/File Metadata for the list view (Client-side AJAX call) ---
        [HttpGet]
        public IActionResult GetPatientFilesMetadata()
        {
            string dataRootPath = Path.Combine(_hostingEnvironment.ContentRootPath, DATA_FOLDER_NAME);
            var patientGroups = new List<PatientGroup>();

            if (!Directory.Exists(dataRootPath))
            {
                _logger.LogWarning("GTLB-Data folder not found at: {DataRootPath}", dataRootPath);
                return Json(patientGroups);
            }

            var patientDirectories = Directory.GetDirectories(dataRootPath);

            if (patientDirectories.Length == 0)
            {
                var filesInRoot = Directory.GetFiles(dataRootPath, "*.csv");
                var grouped = filesInRoot.GroupBy(fp =>
                {
                    var fname = Path.GetFileNameWithoutExtension(fp);
                    var parts = fname.Split('_');
                    return parts.Length > 0 ? parts[0] : fname;
                });

                foreach (var grp in grouped)
                {
                    string patientId = grp.Key;
                    var group = new PatientGroup { PatientId = patientId };
                    foreach (var filePath in grp)
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
                    if (group.Files.Any()) patientGroups.Add(group);
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
                    {
                        patientGroups.Add(group);
                    }
                }
            }
            
            return Json(patientGroups);
        }

        // --- Action to get the FULL Heatmap Partial View (Client-side AJAX call) ---
        [HttpGet]
        public IActionResult GetHeatmapPartial(string patientId, string fileName)
        {
            string baseDataPath = Path.Combine(_hostingEnvironment.ContentRootPath, DATA_FOLDER_NAME);
            string patientFolderPath = Path.Combine(baseDataPath, patientId ?? string.Empty);
            string fullPath;
            if (!string.IsNullOrEmpty(patientId) && Directory.Exists(patientFolderPath))
            {
                fullPath = Path.Combine(patientFolderPath, fileName);
            }
            else
            {
                fullPath = Path.Combine(baseDataPath, fileName);
            }

            try
            {
                int[,] requestedMatrix = LoadSingleMatrix(fullPath);
                
                var matrixAsList = new List<List<int>>();
                for (int i = 0; i < MATRIX_SIZE; i++)
                {
                    matrixAsList.Add(new List<int>());
                    for (int j = 0; j < MATRIX_SIZE; j++)
                    {
                        matrixAsList[i].Add(requestedMatrix[i, j]);
                    }
                }

                var model = new HeatmapData { PressureMatrix = matrixAsList, TotalMatrices = 1 };
                CalculateMetrics(model); 

                return PartialView("_HeatmapPartial", model);
            }
            catch (FileNotFoundException)
            {
                _logger.LogWarning("Requested file not found: {FullPath}", fullPath);
                return NotFound($"File not found: {fullPath}.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing data for file {FullPath}", fullPath);
                return StatusCode(500, $"Error processing data: {ex.Message}");
            }
        }

        // --- Action to save a comment ---
        [HttpPost]
        public IActionResult SaveComment(string patientId, string fileName, string comment)
        {
            if (string.IsNullOrWhiteSpace(patientId) || string.IsNullOrWhiteSpace(fileName) || string.IsNullOrWhiteSpace(comment))
            {
                return BadRequest("Missing required data.");
            }

            try
            {
                string commentsRootPath = Path.Combine(_hostingEnvironment.ContentRootPath, COMMENTS_FOLDER_NAME);
                Directory.CreateDirectory(commentsRootPath);
                string commentFilePath = Path.Combine(commentsRootPath, $"{patientId}_comments.csv");

                string timestamp = DateTime.UtcNow.ToString("o"); 
                string cleanComment = $"\"{comment.Replace("\"", "\"\"")}\"";
                string line = $"{timestamp},{fileName},{cleanComment}{Environment.NewLine}";

                bool fileExists = System.IO.File.Exists(commentFilePath);
                using (StreamWriter sw = new StreamWriter(commentFilePath, append: true))
                {
                    if (!fileExists)
                    {
                        sw.Write("Timestamp,FileName,Comment" + Environment.NewLine);
                    }
                    sw.Write(line);
                }

                return Ok(); 
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving comment for patient {PatientId}", patientId);
                return StatusCode(500, "An error occurred while saving the comment.");
            }
        }

        // --- NEW: Action to get ALL comments ---
        [HttpGet]
        public IActionResult GetAllComments()
        {
            var allComments = new List<PatientCommentViewModel>();
            string commentsRootPath = Path.Combine(_hostingEnvironment.ContentRootPath, COMMENTS_FOLDER_NAME);

            if (!Directory.Exists(commentsRootPath))
            {
                return Json(allComments); 
            }

            try
            {
                var commentFiles = Directory.GetFiles(commentsRootPath, "*_comments.csv");

                foreach (var filePath in commentFiles)
                {
                    string patientId = Path.GetFileNameWithoutExtension(filePath).Split('_')[0];
                    
                    var lines = System.IO.File.ReadLines(filePath).Skip(1);

                    foreach (var line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        
                        var firstCommaIndex = line.IndexOf(',');
                        var secondCommaIndex = line.IndexOf(',', firstCommaIndex + 1);

                        if (firstCommaIndex == -1 || secondCommaIndex == -1)
                        {
                            _logger.LogWarning("Skipping malformed comment line in {FilePath}: {Line}", filePath, line);
                            continue;
                        }
                        
                        try
                        {
                            var timestamp = line.Substring(0, firstCommaIndex);
                            var fileName = line.Substring(firstCommaIndex + 1, secondCommaIndex - firstCommaIndex - 1);
                            var comment = line.Substring(secondCommaIndex + 1);

                            if (comment.StartsWith("\"") && comment.EndsWith("\""))
                            {
                                comment = comment.Substring(1, comment.Length - 2).Replace("\"\"", "\"");
                            }

                            allComments.Add(new PatientCommentViewModel
                            {
                                PatientId = patientId,
                                Timestamp = timestamp,
                                FileName = fileName,
                                Comment = comment
                            });
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error parsing comment line in {FilePath}: {Line}", filePath, line);
                        }
                    }
                }

                return Json(allComments.OrderByDescending(c => c.Timestamp));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read all comments from {CommentsRootPath}", commentsRootPath);
                return StatusCode(500, "Error reading comments.");
            }
        }

        // --- PRIVATE HELPER METHODS ---

        private int[,] LoadSingleMatrix(string path)
        {
            if (!System.IO.File.Exists(path))
            {
                throw new FileNotFoundException("The specified data file was not found.", path);
            }

            var lines = System.IO.File.ReadLines(path).Take(MATRIX_SIZE).ToArray();
            int[,] mat = new int[MATRIX_SIZE, MATRIX_SIZE];

            for (int i = 0; i < lines.Length; i++)
            {
                var row = lines[i].Split(',');
                for (int j = 0; j < MATRIX_SIZE; j++)
                {
                    if (row.Length > j && int.TryParse(row[j].Trim(), out int val))
                    {
                        mat[i, j] = val;
                    }
                    else
                    {
                        mat[i, j] = 0;
                    }
                }
            }
            return mat;
        }

        private PatientFile ReadAndSummarizeCsv(string path, string patientId, string fileName)
        {
            var lines = System.IO.File.ReadLines(path).Take(MATRIX_SIZE).ToList();

            int maxPressure = 0;
            int contactCount = 0;
            const int TOTAL_PIXELS = MATRIX_SIZE * MATRIX_SIZE;
            const int MATRIX_PREVIEW_SIZE = 8;

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

                        if (i % (MATRIX_SIZE / MATRIX_PREVIEW_SIZE) == 0 && j % (MATRIX_SIZE / MATRIX_PREVIEW_SIZE) == 0)
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
            float contactAreaPercentFloat = (float)Math.Round((double)contactCount / TOTAL_PIXELS * 100.0);
            model.ContactAreaPercent = (int)contactAreaPercentFloat;
            model.IsAlertGenerated = maxPressure >= ALERT_THRESHOLD;
        }

        // --- Utility Actions ---

        public IActionResult Search(string searchQuery)
        {
            return RedirectToAction("Patient");
        }

        public new IActionResult SignOut()
        {
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult GetPatientFiles()
        {
            string folderPath = Path.Combine(_hostingEnvironment.WebRootPath, "GTLBData");
            var patients = new List<string>();

            if (Directory.Exists(folderPath))
            {
                var files = Directory.GetFiles(folderPath, "*.csv");

                patients = files.Select(file =>
                {
                    var name = Path.GetFileNameWithoutExtension(file);
                    var parts = name.Split('_');
                    return parts.Length > 0 ? parts[0] : name;
                }).Distinct().ToList();
            }

            return Json(patients);
        }

[HttpPost]
public IActionResult AddClinician([FromBody] ClinicianInput clinician)
{
    try
    {
        if (string.IsNullOrWhiteSpace(clinician.FirstName) || string.IsNullOrWhiteSpace(clinician.LastName))
            return Json(new { success = false, message = "First and last name required." });

        if (string.IsNullOrWhiteSpace(clinician.IdNumber) || clinician.IdNumber.Length != 10)
            return Json(new { success = false, message = "ID number must be 10 digits." });

        // Generate random GTID
        var random = new Random();
        const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
        var gtid = new string(Enumerable.Repeat(chars, 8)
            .Select(s => s[random.Next(s.Length)]).ToArray());

        // Create folder
        string clinicianFolder = Path.Combine(_hostingEnvironment.WebRootPath, "clinicianDetails");
        if (!Directory.Exists(clinicianFolder))
            Directory.CreateDirectory(clinicianFolder);

        string filePath = Path.Combine(clinicianFolder, $"{gtid}.txt");

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"GTID: {gtid}");
        sb.AppendLine($"Title: {clinician.Title}");
        sb.AppendLine($"First Name: {clinician.FirstName}");
        sb.AppendLine($"Middle Name: {clinician.MiddleName}");
        sb.AppendLine($"Last Name: {clinician.LastName}");
        sb.AppendLine($"ID Number: {clinician.IdNumber}");
        sb.AppendLine($"Created At: {DateTime.Now}");

        System.IO.File.WriteAllText(filePath, sb.ToString());

        return Json(new { success = true, gtid });
    }
    catch (Exception ex)
    {
        Console.WriteLine(ex.Message);
        return Json(new { success = false, message = "Error saving clinician." });
    }
}

        public class ClinicianInput
        {
            public string Title { get; set; } = string.Empty;
            public string FirstName { get; set; } = string.Empty;
            public string MiddleName { get; set; } = string.Empty;
            public string LastName { get; set; } = string.Empty;
            public string IdNumber { get; set; } = string.Empty;
        }
}
}