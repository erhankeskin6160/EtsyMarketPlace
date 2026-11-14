using System.Collections.Generic;
using System.Linq;

namespace EtsyMarketPlace.Application.Common.Models;

public class ValidationErrorResponse : ServiceResponse
{
    public IDictionary<string, string[]> Errors { get; set; } = new Dictionary<string, string[]>();

    public ValidationErrorResponse(IDictionary<string, string[]> errors)
    {
        Success = false;
        Message = errors.Values.SelectMany(x => x).FirstOrDefault(m => !string.IsNullOrWhiteSpace(m))
            ?? ResponseMessage.ValidationError;
        StatusCode = 400;
        Errors = errors;
    }
}
