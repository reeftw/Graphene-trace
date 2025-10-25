using System.Collections.Generic;

namespace GrapheneTrace.Models // <-- IMPORTANT: Ensure this namespace matches your project!
{
    public class HeatmapData
    {
        // The 32x32 pressure matrix (stored as a list of lists of integers for Razor view ease)
        public List<List<int>> PressureMatrix { get; set; } = new List<List<int>>();

        // Slider/Metadata properties
        public int MatrixIndex { get; set; }
        public int TotalMatrices { get; set; }

        // Key metrics calculated server-side (as per case study)
        public int PeakPressureIndex { get; set; } 
        public int ContactAreaPercent { get; set; } 
        public bool IsAlertGenerated { get; set; }
        
        // Metadata for the Clinician view (optional but good practice)
        public string GTLBData { get; set; } = string.Empty;
        public string PatientId { get; set; } = string.Empty;
    }
}
