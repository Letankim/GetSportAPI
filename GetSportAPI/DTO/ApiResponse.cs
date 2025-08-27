using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GetSportAPI.DTO
{
    public class ApiResponse<T>
    {
        public int StatusCode { get; set; }
        public string Status { get; set; }
        public string Message { get; set; }
        public IDictionary<string, string[]>? Errors { get; set; }
        public T Data { get; set; }

        public ApiResponse(int statusCode, string status, string message, IDictionary<string, string[]>? errors = null, T data = default)
        {
            StatusCode = statusCode;
            Status = status;
            Message = message;
            Errors = errors;
            Data = data;
        }
    }
}
