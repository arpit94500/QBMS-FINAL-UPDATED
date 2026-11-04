namespace QBMS.Application.Models
{
    public class HrcdfLookupResult
    {
        public bool Success { get; set; }

        public string Name { get; set; }

        public string Message { get; set; }

        public static HrcdfLookupResult Ok(string name)
        {
            return new HrcdfLookupResult { Success = true, Name = name };
        }

        public static HrcdfLookupResult Fail(string message)
        {
            return new HrcdfLookupResult { Success = false, Message = message };
        }
    }
}
