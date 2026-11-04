using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace QBMS1.Services
{
    /// <summary>
    /// Until a user has replaced their default / admin-reset password, every request
    /// except the Change Password page and Logout is redirected to Change Password.
    /// (Static files are served earlier in the pipeline and are not affected.)
    /// </summary>
    public class ForcePasswordChangeMiddleware
    {
        private static readonly PathString ChangePasswordPath = new PathString("/Account/ChangePassword");
        private static readonly PathString LogoutPath = new PathString("/Account/Logout");

        private readonly RequestDelegate _next;

        public ForcePasswordChangeMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var user = context.User;

            if (user != null &&
                user.Identity != null &&
                user.Identity.IsAuthenticated &&
                user.HasClaim(AuthClaimTypes.MustChangePassword, "true"))
            {
                var path = context.Request.Path;

                var allowed =
                    path.StartsWithSegments(ChangePasswordPath, StringComparison.OrdinalIgnoreCase) ||
                    path.StartsWithSegments(LogoutPath, StringComparison.OrdinalIgnoreCase);

                if (!allowed)
                {
                    context.Response.Redirect(
                        context.Request.PathBase.Add(ChangePasswordPath).ToUriComponent());
                    return;
                }
            }

            await _next(context);
        }
    }
}
