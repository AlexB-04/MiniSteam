namespace MiniSteam.Services
{
    public enum ServiceResultStatus
    {
        Success,
        NotFound,
        Forbidden,
        AlreadyOwned,
        Conflict,
        Empty,
        InvalidOperation
    }

    public class ServiceResult<T>
    {
        public ServiceResultStatus Status { get; set; }
        public T? Value { get; set; }
        public string? Message { get; set; }

        public bool Succeeded => Status == ServiceResultStatus.Success;

        public static ServiceResult<T> Success(T value)
        {
            return new ServiceResult<T>
            {
                Status = ServiceResultStatus.Success,
                Value = value
            };
        }

        public static ServiceResult<T> Fail(ServiceResultStatus status, string? message = null)
        {
            return new ServiceResult<T>
            {
                Status = status,
                Message = message
            };
        }
    }
}
