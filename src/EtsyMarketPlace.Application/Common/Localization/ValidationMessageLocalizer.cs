using System;
using System.Globalization;

namespace EtsyMarketPlace.Application.Common.Localization;

/// <summary>
/// Resolves localized validation messages dynamically per request based on <see cref="CultureInfo.CurrentUICulture"/>.
/// Always invoke inside FluentValidation's lazy <c>.WithMessage(_ =&gt; ...)</c> overload so singleton/scoped validators
/// evaluate the active request culture on every validation call.
/// </summary>
public static class ValidationMessageLocalizer
{
    public static bool IsTurkish =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("tr", StringComparison.OrdinalIgnoreCase);

    public static string Pick(string tr, string en) => IsTurkish ? tr : en;

    public static string ValidationFailedSummary =>
        Pick("Validasyon kuralları doğrulanamadı.", "Validation rules failed.");
}
