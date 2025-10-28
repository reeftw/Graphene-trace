using Microsoft.AspNetCore.Mvc;
using GrapheneTrace.Models;
using GrapheneTrace.ViewModels;
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
        // CRITICAL FIX: Looks for the GTLB-Data folder inside the wwwroot folder
        private const string DATA_FOLDER_NAME = "wwwroot/GTLBData"; 
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
            // Path is constructed: ContentRoot/wwwroot/GTLB-Data
            string dataRootPath = Path.Combine(_hostingEnvironment.ContentRootPath, DATA_FOLDER_NAME);
            var patientGroups = new List<PatientGroup>();

            if (!Directory.Exists(dataRootPath))
            {
                // Graceful failure: return empty JSON list if folder is missing
                _logger.LogWarning("GTLB-Data folder not found at: {DataRootPath}", dataRootPath);
                return Json(patientGroups);
            }

            // Get all subdirectories (which represent Patient IDs)
            var patientDirectories = Directory.GetDirectories(dataRootPath);

            // If there are no subdirectories, support CSV files placed directly in the GTLBData folder
            if (patientDirectories.Length == 0)
            {
                var filesInRoot = Directory.GetFiles(dataRootPath, "*.csv");
                // Group files by prefix before first underscore (e.g. patientId_date.csv -> patientId)
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

                    // Get all CSV files inside the patient folder
                    var files = Directory.GetFiles(patientDir, "*.csv");

                    foreach (var filePath in files)
                    {
                        try
                        {
                            // Read and process the first few lines for the summary/mini-map
                            var summaryData = ReadAndSummarizeCsv(filePath, patientId, Path.GetFileName(filePath));
                            group.Files.Add(summaryData);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error processing file {FilePath}", filePath);
                            // Skip problematic file
                        }
                    }
                    // Only add patient group if files were successfully processed
                    if (group.Files.Any())
                    {
                        patientGroups.Add(group);
                    }
                }
            }

            // This JSON response is what your clinician.html JavaScript uses to draw the list
            return Json(patientGroups);
        }

        // --- Action to get the FULL Heatmap Partial View (Client-side AJAX call) ---
        [HttpGet]
        public IActionResult GetHeatmapPartial(string patientId, string fileName)
        {
            // Construct the path: prefer ContentRoot/wwwroot/GTLBData/PatientId/FileName.csv
            // but fall back to ContentRoot/wwwroot/GTLBData/FileName.csv if patient folders are not used
            string baseDataPath = Path.Combine(_hostingEnvironment.ContentRootPath, DATA_FOLDER_NAME);
            string patientFolderPath = Path.Combine(baseDataPath, patientId ?? string.Empty);
            string fullPath;
            if (!string.IsNullOrEmpty(patientId) && Directory.Exists(patientFolderPath))
            {
                fullPath = Path.Combine(patientFolderPath, fileName);
            }
            else
            {
                // fallback: files might be directly under GTLBData with names like <patient>_date.csv
                fullPath = Path.Combine(baseDataPath, fileName);
            }
            
            try
            {
                int[,] requestedMatrix = LoadSingleMatrix(fullPath); 
                
                // Convert 2D array (int[,]) to List of Lists (List<List<int>>) for model compatibility
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
                CalculateMetrics(model); // Calculate the final metrics
                
                return PartialView("_HeatmapPartial", model); 
            }
            catch (FileNotFoundException)
            {
                _logger.LogWarning("Requested file not found: {FullPath}", fullPath);
                return NotFound($"File not found: {fullPath}. Check file placement in GTLB-Data/{patientId}/");
            }
            catch (Exception ex)
            {
                // General error catching for parsing or reading issues
                _logger.LogError(ex, "Error processing data for file {FullPath}", fullPath);
                return StatusCode(500, $"Error processing data: {ex.Message}");
            }
        }

        // --- PRIVATE HELPER METHODS (Required for core functionality) ---

        // Loads only a SINGLE 32x32 matrix from a given file path.
        private int[,] LoadSingleMatrix(string path)
        {
            if (!System.IO.File.Exists(path))
            {
                throw new FileNotFoundException("The specified data file was not found.", path);
            }

            // Use ReadAllLines and Take(32) to get only one 32x32 frame
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
                         mat[i, j] = 0; // Default to 0 if parsing fails or column is missing
                    }
                }
            }
            return mat;
        }

        // Reads the CSV file and calculates metrics for the summary list view
        private PatientFile ReadAndSummarizeCsv(string path, string patientId, string fileName)
        {
            // CS0168 Fix: Exception variable is now used in Console.WriteLine or logging. (Not applicable to this method, but shown in others)
            
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
                        // Full matrix analysis for metrics
                        maxPressure = Math.Max(maxPressure, val);
                        if (val >= MIN_CONTACT_PRESSURE) contactCount++;

                        // Mini-map matrix generation (Subsample by a factor of 32/8 = 4)
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
            
            // Calculate final metrics
            bool isAlert = maxPressure >= ALERT_THRESHOLD;
            float contactAreaPercentFloat = (float)Math.Round((double)contactCount / TOTAL_PIXELS * 100.0);

            return new PatientFile
            {
                FileName = fileName,
                PeakPressure = maxPressure,
                ContactArea = (int)contactAreaPercentFloat, // CS0266 FIX: Explicit cast to int
                IsAlert = isAlert,
                SmallMatrix = miniMatrix // The 8x8 subset
            };
        }

        // Calculates Peak Pressure, Contact Area %, and Alert Status (for full view model)
        private void CalculateMetrics(HeatmapData model)
        {
            int maxPressure = 0;
            int contactCount = 0;
            const int TOTAL_PIXELS = MATRIX_SIZE * MATRIX_SIZE;
            
            foreach(var row in model.PressureMatrix)
            {
                foreach(var val in row)
                {
                    maxPressure = Math.Max(maxPressure, val);
                    if (val >= MIN_CONTACT_PRESSURE) contactCount++;
                }
            }
            
            model.PeakPressureIndex = maxPressure;
            float contactAreaPercentFloat = (float)Math.Round((double)contactCount / TOTAL_PIXELS * 100.0);
            model.ContactAreaPercent = (int)contactAreaPercentFloat; // CS0266 FIX: Explicit cast to int
            model.IsAlertGenerated = maxPressure >= ALERT_THRESHOLD;
        }
        public IActionResult Search(string searchQuery)
        {
            // In a real app: log, redirect to a search results page, or update the current view
            // For now, just redirects back to the patient homepage
            return RedirectToAction("Patient");

        }
        public new IActionResult SignOut()
        {
            // In a real app: clear session, cookies, etc.
            // For now, just redirects back to the homepage
            return RedirectToAction("Index", "Home"); // Example sign-out destination
        }
    }
}