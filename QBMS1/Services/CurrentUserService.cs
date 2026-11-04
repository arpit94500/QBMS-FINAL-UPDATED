using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using QBMS.Application.Interfaces;

namespace QBMS1.Services
{
    public class CurrentUserService : ICurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CurrentUserService(
            IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        private ClaimsPrincipal User
        {
            get
            {
                return _httpContextAccessor
                    .HttpContext
                    ?.User;
            }
        }

        public bool IsAuthenticated
        {
            get
            {
                return User != null &&
                       User.Identity != null &&
                       User.Identity.IsAuthenticated;
            }
        }

        public long UserId
        {
            get
            {
                var value = User?
                    .FindFirst(ClaimTypes.NameIdentifier)
                    ?.Value;

                long.TryParse(value, out var userId);

                return userId;
            }
        }

        public string PersonnelNo
        {
            get
            {
                return User?
                    .FindFirst("PersonnelNo")
                    ?.Value;
            }
        }

        public string UserName
        {
            get
            {
                return User?
                    .FindFirst(ClaimTypes.Name)
                    ?.Value;
            }
        }

        public string Role
        {
            get
            {
                return User?
                    .FindFirst(ClaimTypes.Role)
                    ?.Value;
            }
        }
    }
}
