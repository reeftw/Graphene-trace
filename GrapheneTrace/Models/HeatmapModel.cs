using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace GrapheneTrace.Models
{
    /// <summary>
    /// Represents pressure sensor data and analysis for a patient's pressure map reading.
    /// This model is used to display and analyze 32x32 pressure matrices in the heatmap view.
    /// </summary>
    public class HeatmapData
    {
        /// <summary>
        /// The 32x32 pressure matrix stored as a list of lists for Razor view compatibility.
        /// Each value represents pressure in mmHg at a specific sensor point.
        /// </summary>
        [Required]
        public List<List<int>> PressureMatrix { get; set; } = new List<List<int>>();

        /// <summary>
        /// Current matrix index when viewing multiple frames of pressure data.
        /// </summary>
        [Range(0, int.MaxValue, ErrorMessage = "Matrix index must be non-negative")]
        public int MatrixIndex { get; set; }

        /// <summary>
        /// Total number of pressure matrices available in the dataset.
        /// </summary>
        [Range(1, int.MaxValue, ErrorMessage = "Must have at least one pressure matrix")]
        public int TotalMatrices { get; set; }

        /// <summary>
        /// Maximum pressure value found in the matrix, measured in mmHg.
        /// Used for alert generation and clinical assessment.
        /// </summary>
        [Range(0, int.MaxValue, ErrorMessage = "Peak pressure must be non-negative")]
        public int PeakPressureIndex { get; set; }

        /// <summary>
        /// Percentage of sensor points registering pressure above the minimum threshold.
        /// Indicates the total contact area between patient and surface.
        /// </summary>
        [Range(0, 100, ErrorMessage = "Contact area percentage must be between 0 and 100")]
        public int ContactAreaPercent { get; set; }

        /// <summary>
        /// Indicates whether the pressure readings exceed safe thresholds.
        /// True if PeakPressureIndex >= ALERT_THRESHOLD (200 mmHg).
        /// </summary>
        public bool IsAlertGenerated { get; set; }
        
        /// <summary>
        /// Reference to the source data file name.
        /// </summary>
        [Required(ErrorMessage = "Data source file name is required")]
        [RegularExpression(@"^[\w\-. ]+$", ErrorMessage = "Invalid file name format")]
        public string GTLBData { get; set; } = string.Empty;

        /// <summary>
        /// Unique identifier for the patient associated with this pressure data.
        /// </summary>
        [Required(ErrorMessage = "Patient ID is required")]
        [RegularExpression(@"^[a-fA-F0-9]{8}$", ErrorMessage = "Invalid patient ID format")]
        public string PatientId { get; set; } = string.Empty;
    }
}
