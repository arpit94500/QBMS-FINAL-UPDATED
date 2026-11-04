using System.Threading.Tasks;
using QBMS.Application.Models;

namespace QBMS.Application.Interfaces
{
    public interface IAuthenticationService
    {
        Task<LoggedInUserModel> ValidateUserAsync(
            string personnelNo,
            string password);

        Task<PasswordChangeResult> ChangePasswordAsync(
            string personnelNo,
            string currentPassword,
            string newPassword);
    }
}
