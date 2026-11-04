namespace QBMS.Application.Models
{
    public class SecuritySettings
    {
        /// <summary>
        /// Password assigned to newly created users and to users whose
        /// password is reset by an admin. They must change it at next login.
        /// Overridable via appsettings.json -> "Security:DefaultPassword".
        /// </summary>
        public string DefaultPassword { get; set; } = "Admin@123";
    }
}
