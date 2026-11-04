using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QBMS.Application.Interfaces;
using QBMS.Application.Models;
using QBMS1.Services;

namespace QBMS1.Controllers
{
    public class AccountController : Controller
    {
        private readonly QBMS.Application.Interfaces.IAuthenticationService
            _authenticationService;

        private readonly ILoginPasswordCrypto _loginCrypto;
        private readonly IActivityLogRepository _activityLog;

        public AccountController(
            QBMS.Application.Interfaces.IAuthenticationService authenticationService,
            ILoginPasswordCrypto loginCrypto,
            IActivityLogRepository activityLog)
        {
            _activityLog = activityLog;
            _authenticationService =
                authenticationService;

            _loginCrypto = loginCrypto;
        }

        private Task LogLoginAsync(string activity, string personnelNo, string userName, string role, long? userId, bool success, string details)
        {
            return _activityLog.InsertAsync(new ActivityLogEntry
            {
                UserId = userId,
                PersonnelNo = personnelNo,
                UserName = userName,
                Role = role,
                Activity = activity,
                HttpMethod = Request.Method,
                Path = Request.Path.Value,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                IsSuccess = success,
                Details = details
            });
        }

        // Public key + one-time challenge the login page needs to encrypt the password in the browser.
        private IActionResult LoginView(LoginViewModel model = null)
        {
            ViewBag.LoginPublicKey = _loginCrypto.PublicKeyBase64;
            ViewBag.LoginChallenge = _loginCrypto.IssueChallenge();

            if (model != null)
            {
                // never send the encrypted password back to the page
                model.EncryptedPassword = null;
                ModelState.Remove(nameof(LoginViewModel.EncryptedPassword));
            }

            return model == null ? View() : View(model);
        }

        [HttpGet]
        public IActionResult Login()
        {
            return LoginView();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(
            LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return LoginView(model);
            }

            // The password arrives RSA-encrypted from the browser; decrypt it here (server side only).
            string password;

            if (!_loginCrypto.TryDecrypt(model.EncryptedPassword, out password))
            {
                await LogLoginAsync("LOGIN_FAILED", model.PersonnelNo, null, null, null, false, "Password could not be read (expired page)");

                ModelState.AddModelError(
                    "",
                    "The login page has expired or the password could not be read. Please try again.");

                return LoginView(model);
            }

            var user =
                await _authenticationService
                    .ValidateUserAsync(
                        model.PersonnelNo,
                        password);

            if (user == null)
            {
                await LogLoginAsync("LOGIN_FAILED", model.PersonnelNo, null, null, null, false, "Invalid personnel no or password");

                ModelState.AddModelError(
                    "",
                    "Invalid Personnel No or Password.");

                return LoginView(model);
            }

            var claims =
                new List<Claim>
                {
                    new Claim(
                        ClaimTypes.NameIdentifier,
                        user.UserId.ToString()),

                    new Claim(
                        ClaimTypes.Name,
                        user.UserName),

                    new Claim(
                        AuthClaimTypes.PersonnelNo,
                        user.PersonnelNo),

                    new Claim(
                        ClaimTypes.Role,
                        user.RoleName)
                };

            if (user.MustChangePassword)
            {
                claims.Add(
                    new Claim(
                        AuthClaimTypes.MustChangePassword,
                        "true"));
            }

            var identity =
                new ClaimsIdentity(
                    claims,
                    CookieAuthenticationDefaults
                        .AuthenticationScheme);

            var principal =
                new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults
                    .AuthenticationScheme,
                principal);

            await LogLoginAsync("LOGIN_SUCCESS", user.PersonnelNo, user.UserName, user.RoleName, user.UserId, true, null);

            // Default password / admin reset: the user must set their own password first.
            if (user.MustChangePassword)
            {
                return RedirectToAction(
                    nameof(ChangePassword));
            }

            var redirect = RedirectToDashboard(user.RoleName);

            if (redirect != null)
            {
                return redirect;
            }

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            ModelState.AddModelError(
                "",
                "Invalid user role.");

            return LoginView(model);
        }

        [HttpGet]
        [Authorize]
        public IActionResult ChangePassword()
        {
            ViewBag.IsFirstLogin = IsPasswordChangeRequired();

            return View(new ChangePasswordViewModel());
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(
            ChangePasswordViewModel model)
        {
            ViewBag.IsFirstLogin = IsPasswordChangeRequired();

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var personnelNo =
                User.FindFirst(AuthClaimTypes.PersonnelNo)?.Value;

            var result =
                await _authenticationService
                    .ChangePasswordAsync(
                        personnelNo,
                        model.CurrentPassword,
                        model.NewPassword);

            if (!result.Success)
            {
                ModelState.AddModelError(
                    "",
                    result.Message);

                return View(model);
            }

            var roleName =
                User.FindFirst(ClaimTypes.Role)?.Value;

            // Re-issue the login cookie without the "must change password" flag.
            var claims =
                User.Claims
                    .Where(c => c.Type != AuthClaimTypes.MustChangePassword)
                    .ToList();

            var identity =
                new ClaimsIdentity(
                    claims,
                    CookieAuthenticationDefaults
                        .AuthenticationScheme);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults
                    .AuthenticationScheme,
                new ClaimsPrincipal(identity));

            TempData["SuccessMessage"] =
                "Password changed successfully.";

            var redirect = RedirectToDashboard(roleName);

            if (redirect != null)
            {
                return redirect;
            }

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            return RedirectToAction(
                nameof(Login));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            return RedirectToAction(
                "Login","Account");
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        private bool IsPasswordChangeRequired()
        {
            return User.HasClaim(
                AuthClaimTypes.MustChangePassword,
                "true");
        }

        private IActionResult RedirectToDashboard(string roleName)
        {
            if (roleName == "ADMIN")
            {
                return RedirectToAction(
                    "Dashboard",
                    "Admin");
            }

            if (roleName == "SETTER")
            {
                return RedirectToAction(
                    "Dashboard",
                    "Setter");
            }

            if (roleName == "VETTER")
            {
                return RedirectToAction(
                    "Dashboard",
                    "Vetter");
            }

            return null;
        }
    }
}
