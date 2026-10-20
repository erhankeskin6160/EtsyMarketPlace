using System.Collections.Generic;

namespace EtsyMarketPlace.Application.Common.Models;

public class ValidationErrorResponse : ServiceResponse
{
    public IDictionary<string, string[]> Errors { get; set; } = new Dictionary<string, string[]>();

    public ValidationErrorResponse(IDictionary<string, string[]> errors)
    {
        Success = false;
        Message = ResponseMessage.ValidationError;
        StatusCode = 400;
        Errors = errors;
    }
}
