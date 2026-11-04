using System.ComponentModel.DataAnnotations;

namespace QBMS.Application.Models
{
    public class LoginViewModel
    {
        [Required]
        [Display(Name = "Personnel No")]
        public string PersonnelNo { get; set; }

        /// <summary>
        /// The browser no longer posts the plain password. It posts this RSA-encrypted value
        /// (see wwwroot/js/login-crypto.js and QBMS1.Services.LoginPasswordCrypto).
        /// </summary>
        [Required(ErrorMessage = "Enter the password.")]
        public string EncryptedPassword { get; set; }
    }
}
