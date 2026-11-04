using System.Threading.Tasks;
using QBMS.Application.Models;

namespace QBMS.Application.Interfaces
{
    public interface IAuthenticationRepository
    {
        Task<LoggedInUserModel> GetUserAsync(
            string personnelNo);

        Task<bool> SetPasswordAsync(
            long userId,
            string passwordHash,
            bool mustChangePassword);
    }
}
