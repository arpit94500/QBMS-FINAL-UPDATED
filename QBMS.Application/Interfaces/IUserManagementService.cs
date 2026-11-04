using System.Collections.Generic;
using System.Threading.Tasks;
using QBMS.Application.Models;

namespace QBMS.Application.Interfaces
{
    public interface IUserManagementService
    {
        Task<List<UserListViewModel>> GetUsersAsync();

        Task<long> CreateUserAsync(
            CreateUserViewModel model);

        Task<bool> UpdateStatusAsync(
            long userId,
            bool isActive);

        Task<List<DropdownItem>> GetRolesAsync();

        /// <summary>Checks the personnel no is free and fetches the employee name from HRCDF.</summary>
        Task<HrcdfLookupResult> LookupEmployeeAsync(string personnelNo);

        Task<EditUserViewModel> GetUserForEditAsync(long userId);

        Task<bool> UpdateUserAsync(EditUserViewModel model);

        /// <summary>Resets the user's password to the default and forces a change at next login.</summary>
        Task<bool> ResetPasswordAsync(long userId);
    }
}
