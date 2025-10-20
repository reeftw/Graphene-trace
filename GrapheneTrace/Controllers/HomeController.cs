using Microsoft.AspNetCore.Mvc;
using HeatmapMVC.Models;
using System.IO;

namespace HeatmapMVC.Controllers
{

    public class HomeController : Controller
    {

        public IActionResult HeatmapPartial(int index = 0)
        {
            if (index < 0) index = 0;
            if (index >= matrices.Count) index = matrices.Count - 1;

            var data = matrices[index];
            int min = data.Cast<int>().Min();
            int max = data.Cast<int>().Max();

            var model = new HeatmapModel
            {
                Data = data,
                Min = min,
                Max = max,
                MatrixIndex = index,
                TotalMatrices = matrices.Count
            };

            return PartialView("_HeatmapPartial", model); // partial view only
        }

        private static List<int[,]> matrices = new List<int[,]>();
        private static bool loaded = false;

        public IActionResult Index(int index = 0)
        {
            if (!loaded)
            {
                LoadAllMatrices("data.csv"); // load all matrices at startup
                loaded = true;
            }

            if (index < 0) index = 0;
            if (index >= matrices.Count) index = matrices.Count - 1;

            var data = matrices[index];
            int min = data.Cast<int>().Min();
            int max = data.Cast<int>().Max();

            var model = new HeatmapModel
            {
                Data = data,
                Min = min,
                Max = max,
                MatrixIndex = index,
                TotalMatrices = matrices.Count
            };

            return View(model);
        }

        private void LoadAllMatrices(string path)
        {
            var lines = System.IO.File.ReadAllLines(path);
            int matrixSize = 32; // rows per matrix
            int cols = 32;       // fixed columns

            for (int start = 0; start < lines.Length; start += matrixSize)
            {
                int[,] mat = new int[matrixSize, cols];
                for (int i = 0; i < matrixSize; i++)
                {
                    var row = lines[start + i].Split(',');
                    for (int j = 0; j < cols; j++)
                        mat[i, j] = int.Parse(row[j]);
                }
                matrices.Add(mat);
            }
        }
    }
}
