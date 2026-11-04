using System.Collections.Generic;
using System.Threading.Tasks;
using QBMS.Application.Models;

namespace QBMS.Application.Interfaces
{
    public interface IUserManagementRepository
    {
        Task<List<UserListViewModel>> GetUsersAsync();

        /// <summary>Creates the user with must_change_password = TRUE.</summary>
        Task<long> CreateUserAsync(
            CreateUserViewModel model,
            string passwordHash);

        Task<bool> PersonnelNoExistsAsync(
            string personnelNo);

        Task<bool> UpdateStatusAsync(
            long userId,
            bool isActive);
        Task<List<DropdownItem>> GetRolesAsync();

        Task<EditUserViewModel> GetUserByIdAsync(long userId);

        Task<bool> UpdateUserAsync(
            long userId,
            int roleId,
            bool isActive);

        /// <summary>Sets a new password hash and forces a change at next login.</summary>
        Task<bool> ResetPasswordAsync(
            long userId,
            string passwordHash);
    }
}
