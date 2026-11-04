using System.ComponentModel.DataAnnotations;

namespace QBMS.Application.Models
{
    public class ChangePasswordViewModel
    {
        [Required(ErrorMessage = "Current password is required.")]
        [DataType(DataType.Password)]
        [Display(Name = "Current Password")]
        public string CurrentPassword { get; set; }

        [Required(ErrorMessage = "New password is required.")]
        [DataType(DataType.Password)]
        [Display(Name = "New Password")]
        public string NewPassword { get; set; }

        [Required(ErrorMessage = "Please confirm the new password.")]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm New Password")]
        [Compare("NewPassword", ErrorMessage = "New password and confirmation do not match.")]
        public string ConfirmPassword { get; set; }
    }
}
