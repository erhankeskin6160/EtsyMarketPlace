using System.Net;

namespace EtsyMarketPlace.Application.Common.Models;

public class ServiceResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public object? Data { get; set; }
    public int StatusCode { get; set; } = 200;

    public static ServiceResponse CreateSuccess(string message = ResponseMessage.Succeed, object? data = null, int statusCode = 200) =>
        new() { Success = true, Message = message, Data = data, StatusCode = statusCode };

    public static ServiceResponse CreateSuccess(string message, object? data, HttpStatusCode statusCode) =>
        new() { Success = true, Message = message, Data = data, StatusCode = (int)statusCode };

    public static ServiceResponse CreateError(string message = ResponseMessage.Failed, int statusCode = 400) =>
        new() { Success = false, Message = message, StatusCode = statusCode };

    public static ServiceResponse CreateError(string message, HttpStatusCode statusCode) =>
        new() { Success = false, Message = message, StatusCode = (int)statusCode };

    public static ServiceResponse CreateNotFound(string message = ResponseMessage.NotFound, int statusCode = 404) =>
        new() { Success = false, Message = message, StatusCode = statusCode };

    public static ServiceResponse CreateNotFound(string message, HttpStatusCode statusCode) =>
        new() { Success = false, Message = message, StatusCode = (int)statusCode };
}

public class ServiceResponse<T> : ServiceResponse
{
    public new T? Data
    {
        get => base.Data is T typedData ? typedData : default;
        set => base.Data = value;
    }

    public static ServiceResponse<T> CreateSuccess(string message, T data, int statusCode = 200) =>
        new() { Success = true, Message = message, Data = data, StatusCode = statusCode };

    public static ServiceResponse<T> CreateErrorTyped(string message, int statusCode = 400) =>
        new() { Success = false, Message = message, StatusCode = statusCode };
}
