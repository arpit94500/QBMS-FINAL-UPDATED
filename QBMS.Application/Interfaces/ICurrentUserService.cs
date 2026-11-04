namespace QBMS.Application.Interfaces
{
    public interface ICurrentUserService
    {
        long UserId { get; }

        string PersonnelNo { get; }

        string UserName { get; }

        string Role { get; }

        bool IsAuthenticated { get; }
    }
}
