using System.ComponentModel.DataAnnotations;

namespace GrapheneTrace.ViewModels
{
    public class EditAccountViewModel
    {
        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required]
        public string MedicalHistory { get; set; } = string.Empty;
    }
}
