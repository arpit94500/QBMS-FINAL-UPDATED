namespace QBMS1.Services
{
    public static class AuthClaimTypes
    {
        public const string PersonnelNo = "PersonnelNo";

        /// <summary>Present (value "true") while the user still has to change a default/reset password.</summary>
        public const string MustChangePassword = "MustChangePassword";
    }
}
