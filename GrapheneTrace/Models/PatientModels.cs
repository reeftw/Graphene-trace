using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace GrapheneTrace.Models
{
    /// <summary>
    /// Represents a group of pressure data files for a specific patient
    /// </summary>
    public class PatientGroup
    {
        /// <summary>
        /// Unique identifier for the patient
        /// </summary>
        [Required(ErrorMessage = "Patient ID is required")]
        [RegularExpression(@"^[a-fA-F0-9]{8}$", ErrorMessage = "Invalid patient ID format")]
        public required string PatientId { get; set; }

        /// <summary>
        /// Collection of pressure data files associated with this patient
        /// </summary>
        [Required]
        public List<PatientFile> Files { get; set; } = new List<PatientFile>();
    }

    /// <summary>
    /// Represents a single pressure data file with summary metrics
    /// </summary>
    public class PatientFile
    {
        /// <summary>
        /// Name of the data file
        /// </summary>
        [Required(ErrorMessage = "File name is required")]
        [RegularExpression(@"^[\w\-. ]+$", ErrorMessage = "Invalid file name format")]
        public required string FileName { get; set; }

        /// <summary>
        /// Maximum pressure value in the file, measured in mmHg
        /// </summary>
        [Range(0, int.MaxValue, ErrorMessage = "Peak pressure must be non-negative")]
        public int PeakPressure { get; set; }

        /// <summary>
        /// Contact area percentage (0-100)
        /// </summary>
        [Range(0, 100, ErrorMessage = "Contact area percentage must be between 0 and 100")]
        public int ContactArea { get; set; }

        /// <summary>
        /// Indicates if pressure threshold alert is active
        /// </summary>
        public bool IsAlert { get; set; }

        /// <summary>
        /// Reduced size pressure matrix for thumbnail display
        /// </summary>
        [Required]
        public required List<List<int>> SmallMatrix { get; set; } = new();
    }
}