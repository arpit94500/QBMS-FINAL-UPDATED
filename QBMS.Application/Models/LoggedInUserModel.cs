namespace QBMS.Application.Models
{
    public class LoggedInUserModel
    {
        public long UserId { get; set; }

        public string PersonnelNo { get; set; }

        public string UserName { get; set; }

        public string PasswordHash { get; set; }

        public int RoleId { get; set; }

        public string RoleName { get; set; }

        public bool MustChangePassword { get; set; }
    }
}
