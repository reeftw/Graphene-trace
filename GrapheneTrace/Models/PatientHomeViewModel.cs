using GrapheneTrace.Models;
using System.Collections.Generic;

namespace GrapheneTrace.ViewModels
{
    public class PatientHomeViewModel
    {
        // === Pressure Heatmap Data ===
        public HealthMetric PressureData { get; set; }
        public HeatmapData Heatmap { get; set; } = new HeatmapData();

        // === Left Menu Pop-up Data ===
        public string PatientName { get; set; } = "Sarah Connor";
        public List<Clinician> VisitedDoctors { get; set; } = new List<Clinician>();

        // === Right Profile Pop-up Data ===
        public int UserId { get; set; } = 54321;
        public string Email { get; set; } = "d13043b3@gmail.com";
        public string PhoneNumber { get; set; } = "(555) 987-6543";
        public string MedicalHistorySummary { get; set; } = "Diabetes Type 1, Hypertension (Mild)";

        // === Helper Flags ===
        public bool HasNewAlerts { get; set; } = true;

        public PatientHomeViewModel()
        {
            PressureData = new HealthMetric
            {
                Title = "Pressure Heatmap",
                GlanceableInfo = "Latest sensor data visualization."
            };

            VisitedDoctors.Add(new Clinician { Name = "Dr. Ben Carson", Specialization = "Neurologist" });
            VisitedDoctors.Add(new Clinician { Name = "Dr. Lisa Cuddy", Specialization = "Endocrinologist" });
        }
    }
}
