namespace EtsyMarketPlace.Api.Security;

using System.ComponentModel.DataAnnotations;

public sealed class JwtOptions
{
    [Required(ErrorMessage = "Jwt:Secret yapılandırması zorunludur.")]
    [MinLength(32, ErrorMessage = "Jwt:Secret en az 32 karakter uzunluğunda olmalıdır.")]
    public string Secret { get; set; } = string.Empty;

    public string Issuer { get; set; } = "EtsyMarketPlace";

    public string Audience { get; set; } = "EtsyMarketPlaceApp";

    public int ExpiryDays { get; set; } = 7;
}
