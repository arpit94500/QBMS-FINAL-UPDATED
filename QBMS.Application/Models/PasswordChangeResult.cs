namespace QBMS.Application.Models
{
    public class PasswordChangeResult
    {
        public bool Success { get; set; }

        public string Message { get; set; }

        public static PasswordChangeResult Ok()
        {
            return new PasswordChangeResult { Success = true };
        }

        public static PasswordChangeResult Fail(string message)
        {
            return new PasswordChangeResult { Success = false, Message = message };
        }
    }
}
