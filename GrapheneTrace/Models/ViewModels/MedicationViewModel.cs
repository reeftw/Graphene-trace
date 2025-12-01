namespace GrapheneTrace.ViewModels
{
    public class MedicationViewModel
    {
        public string PatientId { get; set; } = string.Empty;

        // You can change this list later if you want dynamic loading
        public List<string> Medications { get; set; } = new List<string>();
    }
}
