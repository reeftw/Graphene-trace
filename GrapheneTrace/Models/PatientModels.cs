using System.Collections.Generic;

namespace GrapheneTrace.Models
{
    public class PatientGroup
    {
        public required string PatientId { get; set; }
        public List<PatientFile> Files { get; set; } = new List<PatientFile>();
    }

    public class PatientFile
    {
        public required string FileName { get; set; }
        public int PeakPressure { get; set; }
        public int ContactArea { get; set; }
        public bool IsAlert { get; set; }
        public required List<List<int>> SmallMatrix { get; set; } = new();
    }
}