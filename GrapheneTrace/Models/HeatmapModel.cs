using System.Collections.Generic;

namespace GrapheneTrace.Models
{
    /// <summary>
    /// Represents pressure sensor data and analysis for a patient's pressure map readings.
    /// Supports multiple frames (time series) from a CSV file.
    /// </summary>
    public class HeatmapData
    {
        // ======== MULTI-FRAME SUPPORT =========

        /// <summary>
        /// All frames loaded from the CSV.
        /// Each frame is a 32x32 matrix: List[row][col] = pressure value.
        /// </summary>
        public List<List<List<int>>> Frames { get; set; } = new();

        /// <summary>
        /// Index of the current frame to display.
        /// </summary>
        public int CurrentFrame { get; set; } = 0;

        /// <summary>
        /// Total number of frames available.
        /// </summary>
        public int TotalFrames => Frames.Count;

        /// <summary>
        /// Convenience property to get the current 32x32 matrix for display.
        /// </summary>
        public List<List<int>> PressureMatrix =>
            Frames.Count == 0 ? new List<List<int>>() : Frames[CurrentFrame];

        // ======== METRICS =========

        /// <summary>
        /// Maximum pressure value found in the current frame.
        /// </summary>
        public int PeakPressureIndex { get; set; }

        /// <summary>
        /// Percentage of sensor points registering pressure above threshold in the current frame.
        /// </summary>
        public int ContactAreaPercent { get; set; }

        /// <summary>
        /// True if pressure exceeds alert threshold in the current frame.
        /// </summary>
        public bool IsAlertGenerated { get; set; }

        // ======== IDENTIFIERS =========

        /// <summary>
        /// Unique identifier for the patient associated with this pressure data.
        /// </summary>
        public string PatientId { get; set; } = string.Empty;

        /// <summary>
        /// Reference to the source data file name (plus optional last comment).
        /// </summary>
        public string GTLBData { get; set; } = string.Empty;
    }
}
