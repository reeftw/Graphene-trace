using System.Collections.Generic;

namespace HeatmapMVC.Models
{
    public class HeatmapModel
    {
        public int[,] Data { get; set; } = new int[0, 0];
        public int Min { get; set; }
        public int Max { get; set; }

        public int MatrixIndex { get; set; }
        public int TotalMatrices { get; set; }

        // Add this property to pass all matrices to the view
        public List<int[,]> AllMatrices { get; set; } = new List<int[,]>();
    }
}
