using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Filters;
using QBMS.Application.Interfaces;
using QBMS.Application.Models;

namespace QBMS1.Services
{
    /// <summary>
    /// Records every controller action performed by a logged-in user: who, what, when, from where, and whether it failed.
    /// Passwords and form bodies are never logged; only the query-string (with secret-looking keys removed).
    /// </summary>
    public class ActivityLogActionFilter : IAsyncActionFilter
    {
        private static readonly string[] SecretKeys = { "password", "pwd", "token", "secret" };
        private readonly IActivityLogRepository _log;

        public ActivityLogActionFilter(IActivityLogRepository log)
        {
            _log = log;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var executed = await next();

            var http = context.HttpContext;
            var user = http.User;
            if (user == null || user.Identity == null || !user.Identity.IsAuthenticated)
            {
                return;                                   // anonymous actions (login page) are logged explicitly
            }

            long id;
            long? userId = long.TryParse(user.FindFirst(ClaimTypes.NameIdentifier)?.Value, out id) ? id : (long?)null;

            var query = string.Join("&", http.Request.Query
                .Where(q => !SecretKeys.Any(s => q.Key.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0))
                .Select(q => q.Key + "=" + q.Value));

            await _log.InsertAsync(new ActivityLogEntry
            {
                UserId = userId,
                PersonnelNo = user.FindFirst(AuthClaimTypes.PersonnelNo)?.Value,
                UserName = user.Identity.Name,
                Role = user.FindFirst(ClaimTypes.Role)?.Value,
                Activity = context.RouteData.Values["controller"] + "/" + context.RouteData.Values["action"],
                HttpMethod = http.Request.Method,
                Path = http.Request.Path.Value,
                QueryString = query,
                IpAddress = http.Connection.RemoteIpAddress?.ToString(),
                IsSuccess = executed.Exception == null || executed.ExceptionHandled,
                Details = executed.Exception == null ? null : "Error: " + executed.Exception.GetType().Name
            });
        }
    }
}
