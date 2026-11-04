using System.Threading.Tasks;
using QBMS.Application.Helpers;
using QBMS.Application.Interfaces;
using QBMS.Application.Models;

namespace QBMS.Infrastructure.Services
{
    public class AuthenticationService
        : IAuthenticationService
    {
        private readonly IAuthenticationRepository
            _repository;

        private readonly SecuritySettings _securitySettings;

        public AuthenticationService(
            IAuthenticationRepository repository,
            SecuritySettings securitySettings)
        {
            _repository = repository;
            _securitySettings = securitySettings;
        }

        public async Task<LoggedInUserModel>
            ValidateUserAsync(
                string personnelNo,
                string password)
        {
            var user = await _repository.GetUserAsync(personnelNo);

            if (user == null)
            {
                return null;
            }

            if (!PasswordHashHelper.Verify(
                    password,
                    user.PasswordHash,
                    out var needsUpgrade))
            {
                return null;
            }

            if (needsUpgrade)
            {
                // Legacy plain-text password: store it hashed from now on.
                // A failure here must not block the login; it is retried next time.
                try
                {
                    await _repository.SetPasswordAsync(
                        user.UserId,
                        PasswordHashHelper.Hash(password),
                        user.MustChangePassword);
                }
                catch
                {
                }
            }

            user.PasswordHash = null;

            return user;
        }

        public async Task<PasswordChangeResult>
            ChangePasswordAsync(
                string personnelNo,
                string currentPassword,
                string newPassword)
        {
            var user =
                await _repository.GetUserAsync(
                    personnelNo);

            if (user == null)
            {
                return PasswordChangeResult.Fail(
                    "User account could not be found.");
            }

            if (!PasswordHashHelper.Verify(
                    currentPassword,
                    user.PasswordHash,
                    out _))
            {
                return PasswordChangeResult.Fail(
                    "Current password is incorrect.");
            }

            var policyError = PasswordPolicy.Validate(
                newPassword,
                currentPassword,
                string.IsNullOrWhiteSpace(_securitySettings.DefaultPassword)
                    ? "Admin@123"
                    : _securitySettings.DefaultPassword);

            if (policyError != null)
            {
                return PasswordChangeResult.Fail(policyError);
            }

            var updated = await _repository.SetPasswordAsync(
                user.UserId,
                PasswordHashHelper.Hash(newPassword),
                false);

            return updated
                ? PasswordChangeResult.Ok()
                : PasswordChangeResult.Fail("Unable to update the password. Please try again.");
        }
    }
}
