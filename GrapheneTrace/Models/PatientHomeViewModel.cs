using GrapheneTrace.Models;
using System.Collections.Generic;

namespace GrapheneTrace.ViewModels
{
    // This model holds ALL data needed for the Patient.cshtml page
    public class PatientHomeViewModel
    {
        // === Main Content Metrics ===
        public HealthMetric PressureData { get; set; }
        public HealthMetric HeartRateData { get; set; }
        public HealthMetric MiscData1 { get; set; }
        public HealthMetric MiscData2 { get; set; }

        // === Left Menu Pop-up Data ===
        public string PatientName { get; set; } = "Sarah Connor";
        public List<Clinician> VisitedDoctors { get; set; } = new List<Clinician>();

        // === Right Profile Pop-up Data ===
        public int UserId { get; set; } = 54321;
        public string Email { get; set; } = "sarah.c@portal.com";
        public string PhoneNumber { get; set; } = "(555) 987-6543";
        public string MedicalHistorySummary { get; set; } = "Diabetes Type 1, Hypertension (Mild)";

        // === Helper Flags / Sample Data Population ===
        public bool HasNewAlerts { get; set; } = true;
        
        public PatientHomeViewModel()
        {
            PressureData = new HealthMetric { Title = "Pressure Heatmap", GlanceableInfo = "Next position change due in 15 minutes." };
            HeartRateData = new HealthMetric { Title = "Heart Rate", GlanceableInfo = "Current: 68 bpm. Avg (Past Hour): 70 bpm." };
            MiscData1 = new HealthMetric { Title = "Activity Steps", GlanceableInfo = "4,500 Steps Today (Goal: 6,000)." };
            MiscData2 = new HealthMetric { Title = "Sleep Duration", GlanceableInfo = "Last night: 7 hours 30 mins." };

            VisitedDoctors.Add(new Clinician { Name = "Dr. Ben Carson", Specialization = "Neurologist" });
            VisitedDoctors.Add(new Clinician { Name = "Dr. Lisa Cuddy", Specialization = "Endocrinologist" });
        }
    }
}