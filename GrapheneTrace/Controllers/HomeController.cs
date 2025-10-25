using Microsoft.AspNetCore.Mvc;
using GrapheneTrace.Models;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Microsoft.AspNetCore.Hosting; 
using System; 

namespace GrapheneTrace.Controllers
{
    // NOTE: Ensure your GrapheneTrace.Models namespace is correct for all models used.
    public class HomeController : Controller
    {
        private readonly IWebHostEnvironment _hostingEnvironment;
        // CRITICAL FIX: Looks for the GTLB-Data folder inside the wwwroot folder
        private const string DATA_FOLDER_NAME = "wwwroot/GTLBData"; 
        private const int MATRIX_SIZE = 32;
        private const int ALERT_THRESHOLD = 200;
        private const int MIN_CONTACT_PRESSURE = 10;

        public HomeController(IWebHostEnvironment hostingEnvironment)
        {
            _hostingEnvironment = hostingEnvironment;
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
            return View();
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
                Console.WriteLine($"ERROR: GTLB-Data folder not found at: {dataRootPath}");
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
                            Console.WriteLine($"Error processing file {filePath}: {ex.Message}");
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
                            Console.WriteLine($"Error processing file {filePath}: {ex.Message}");
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
            // Construct the path: ContentRoot/wwwroot/GTLB-Data/PatientId/FileName.csv
            string fullPath = Path.Combine(_hostingEnvironment.ContentRootPath, DATA_FOLDER_NAME, patientId, fileName);
            
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
                return NotFound($"File not found: {fullPath}. Check file placement in GTLB-Data/{patientId}/");
            }
            catch (Exception ex)
            {
                // General error catching for parsing or reading issues
                Console.WriteLine($"Error processing data: {ex.ToString()}");
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
    }
}