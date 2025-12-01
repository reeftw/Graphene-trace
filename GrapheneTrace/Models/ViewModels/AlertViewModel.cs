namespace GrapheneTrace.ViewModels
{
    public class AlertViewModel
    {
        public string PatientId { get; set; } = string.Empty;
        public List<string> Alerts { get; set; } = new();
    }
}
