namespace GrapheneTrace.ViewModels
{
    public class PatientCommentViewModel
    {
        public string PatientId { get; set; } = string.Empty;
        public string Timestamp { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;
    }
}