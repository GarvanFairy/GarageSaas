namespace GarageSaas.Services.Models
{
    public class ServiceResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }

        public static ServiceResult Ok() => new ServiceResult { Success = true };
        public static ServiceResult Fail(string message) => new ServiceResult { Success = false, ErrorMessage = message };
    }

    public class ServiceResult<T> : ServiceResult
    {
        public T Data { get; set; }

        public static ServiceResult<T> Ok(T data) =>
            new ServiceResult<T> { Success = true, Data = data };

        public new static ServiceResult<T> Fail(string message) =>
            new ServiceResult<T> { Success = false, ErrorMessage = message };
    }
}
