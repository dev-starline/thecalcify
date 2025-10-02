using System.IO.Compression;

namespace CommonDatabase.DTO
{
    public class ApiResponse
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; }
        public object Data { get; set; }

        public static ApiResponse Ok(object data = null, string message = "Success")
        {
            return new ApiResponse
            {
                IsSuccess = true,
                Message = message,
                Data = data ?? Array.Empty<object>()  // use empty array if null
            };
        }

        public static ApiResponse Fail(string message = "Failed", object data = null)
        {
            return new ApiResponse
            {
                IsSuccess = false,
                Message = message,
                Data = data ?? Array.Empty<object>()  // use empty array if null
            };
        }
    }
 }
