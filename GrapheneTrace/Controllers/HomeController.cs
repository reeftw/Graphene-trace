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

    }
}
