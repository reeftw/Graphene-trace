using System.Collections.Generic;

namespace GrapheneTrace.Models
{
    /// <summary>
    /// Represents one pressure heatmap snapshot loaded from GTLBData CSV files.
    /// </summary>
    public class HeatmapData
    {
    
        public List<List<int>> PressureMatrix { get; set; } = new List<List<int>>();

        
        public int MatrixIndex { get; set; }
        public int TotalMatrices { get; set; }

        
        public int PeakPressureIndex { get; set; }
        public int ContactAreaPercent { get; set; }
        public bool IsAlertGenerated { get; set; }

        
        public string GTLBData { get; set; } = string.Empty;
        public string PatientId { get; set; } = string.Empty;
    }
}
