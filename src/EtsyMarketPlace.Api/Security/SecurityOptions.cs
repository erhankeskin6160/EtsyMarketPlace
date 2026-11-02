namespace EtsyMarketPlace.Api.Security;

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

public sealed class SwaggerSecurityOptions
{
    public bool Enabled { get; set; } = true;
    public bool RequireAuth { get; set; } = true;
    public string Username { get; set; } = "admin";
    public string Password { get; set; } = string.Empty;
}

public sealed class SecurityOptions
{
    [Required(ErrorMessage = "Security:ApiKey yapılandırması zorunludur ve boş olamaz.")]
    public string ApiKey { get; set; } = string.Empty;

    public bool RequireHttps { get; set; } = false;

    public List<string> AllowedShopIds { get; set; } = new();

    public SwaggerSecurityOptions Swagger { get; set; } = new();
}
