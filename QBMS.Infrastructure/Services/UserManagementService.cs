using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using QBMS.Application.Interfaces;
using QBMS.Application.Models;

namespace QBMS.Infrastructure.Services
{
    public class UserManagementService
        : IUserManagementService
    {
        private static readonly Regex PersonnelNoPattern =
            new Regex(@"^[A-Za-z0-9_\-]{1,30}$", RegexOptions.Compiled);

        private readonly IUserManagementRepository
            _repository;

        private readonly IHrcdfService _hrcdfService;

        private readonly SecuritySettings _securitySettings;

        public UserManagementService(
            IUserManagementRepository repository,
            IHrcdfService hrcdfService,
            SecuritySettings securitySettings)
        {
            _repository = repository;
            _hrcdfService = hrcdfService;
            _securitySettings = securitySettings;
        }

        private string DefaultPassword
        {
            get
            {
                return string.IsNullOrWhiteSpace(_securitySettings.DefaultPassword)
                    ? "Admin@123"
                    : _securitySettings.DefaultPassword;
            }
        }

        public Task<List<UserListViewModel>>
            GetUsersAsync()
        {
            return _repository.GetUsersAsync();
        }

        public async Task<HrcdfLookupResult>
            LookupEmployeeAsync(string personnelNo)
        {
            personnelNo = (personnelNo ?? string.Empty).Trim();

            if (personnelNo.Length == 0)
            {
                return HrcdfLookupResult.Fail("Enter a Personnel No.");
            }

            if (!PersonnelNoPattern.IsMatch(personnelNo))
            {
                return HrcdfLookupResult.Fail(
                    "Personnel No may contain only letters, digits, '-' and '_'.");
            }

            if (await _repository.PersonnelNoExistsAsync(personnelNo))
            {
                return HrcdfLookupResult.Fail(
                    "A user with this Personnel No already exists.");
            }

            return await _hrcdfService.GetEmployeeAsync(personnelNo);
        }

        public async Task<long>
            CreateUserAsync(
                CreateUserViewModel model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(
                    nameof(model));
            }

            // The name always comes from HRCDF; whatever the browser posted is ignored.
            var lookup = await LookupEmployeeAsync(model.PersonnelNo);

            if (!lookup.Success)
            {
                throw new InvalidOperationException(lookup.Message);
            }

            model.PersonnelNo = model.PersonnelNo.Trim();
            model.UserName = lookup.Name;

            var passwordHash = PasswordHashHelper.Hash(DefaultPassword);

            return await _repository.CreateUserAsync(
                model,
                passwordHash);
        }

        public Task<bool>
            UpdateStatusAsync(
                long userId,
                bool isActive)
        {
            return _repository.UpdateStatusAsync(
                userId,
                isActive);
        }

        public Task<List<DropdownItem>> GetRolesAsync()
        {
            return _repository.GetRolesAsync();
        }

        public Task<EditUserViewModel> GetUserForEditAsync(long userId)
        {
            return _repository.GetUserByIdAsync(userId);
        }

        public async Task<bool> UpdateUserAsync(EditUserViewModel model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            var roles = await _repository.GetRolesAsync();

            if (!roles.Any(r => r.Id == model.RoleId))
            {
                throw new InvalidOperationException("Selected role is invalid.");
            }

            return await _repository.UpdateUserAsync(
                model.UserId,
                model.RoleId,
                model.IsActive);
        }

        public Task<bool> ResetPasswordAsync(long userId)
        {
            return _repository.ResetPasswordAsync(
                userId,
                PasswordHashHelper.Hash(DefaultPassword));
        }
    }
}
