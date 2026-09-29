namespace EtsyMarketPlace.Domain.Shipping;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// ABD ve Kanada eyalet/bölge kodları ve adları için yardımcı sınıf.
/// Aras Global ve diğer kargo API'lerinin beklediği StateCode / StateName formatlarını normalleştirir.
/// </summary>
public static class UsStateHelper
{
    private static readonly Dictionary<string, (string Code, string Name)> StateMap = new(StringComparer.OrdinalIgnoreCase);

    public static readonly IReadOnlyList<(string Code, string Name)> UsStates = new List<(string Code, string Name)>
    {
        ("AL", "Alabama"), ("AK", "Alaska"), ("AZ", "Arizona"), ("AR", "Arkansas"),
        ("CA", "California"), ("CO", "Colorado"), ("CT", "Connecticut"), ("DE", "Delaware"),
        ("FL", "Florida"), ("GA", "Georgia"), ("HI", "Hawaii"), ("ID", "Idaho"),
        ("IL", "Illinois"), ("IN", "Indiana"), ("IA", "Iowa"), ("KS", "Kansas"),
        ("KY", "Kentucky"), ("LA", "Louisiana"), ("ME", "Maine"), ("MD", "Maryland"),
        ("MA", "Massachusetts"), ("MI", "Michigan"), ("MN", "Minnesota"), ("MS", "Mississippi"),
        ("MO", "Missouri"), ("MT", "Montana"), ("NE", "Nebraska"), ("NV", "Nevada"),
        ("NH", "New Hampshire"), ("NJ", "New Jersey"), ("NM", "New Mexico"), ("NY", "New York"),
        ("NC", "North Carolina"), ("ND", "North Dakota"), ("OH", "Ohio"), ("OK", "Oklahoma"),
        ("OR", "Oregon"), ("PA", "Pennsylvania"), ("RI", "Rhode Island"), ("SC", "South Carolina"),
        ("SD", "South Dakota"), ("TN", "Tennessee"), ("TX", "Texas"), ("UT", "Utah"),
        ("VT", "Vermont"), ("VA", "Virginia"), ("WA", "Washington"), ("WV", "West Virginia"),
        ("WI", "Wisconsin"), ("WY", "Wyoming"), ("DC", "District of Columbia"),
        ("PR", "Puerto Rico"), ("VI", "Virgin Islands"), ("GU", "Guam")
    };

    public static readonly IReadOnlyList<(string Code, string Name)> CanadianProvinces = new List<(string Code, string Name)>
    {
        ("AB", "Alberta"), ("BC", "British Columbia"), ("MB", "Manitoba"), ("NB", "New Brunswick"),
        ("NL", "Newfoundland and Labrador"), ("NS", "Nova Scotia"), ("NT", "Northwest Territories"),
        ("NU", "Nunavut"), ("ON", "Ontario"), ("PE", "Prince Edward Island"), ("QC", "Quebec"),
        ("SK", "Saskatchewan"), ("YT", "Yukon")
    };

    static UsStateHelper()
    {
        foreach (var item in UsStates.Concat(CanadianProvinces))
        {
            StateMap[item.Code] = item;
            StateMap[item.Name] = item;
        }
    }

    /// <summary>
    /// Verilen eyalet kodu veya adını (ör. "CA", "California", "california")
    /// standart (Code: "CA", Name: "California") tuple'ına dönüştürür.
    /// Eşleşme bulunamazsa girdiyi her iki alana da kopyalar.
    /// </summary>
    public static (string Code, string Name) ResolveUsOrCaState(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return (string.Empty, string.Empty);
        }

        string trimmed = input.Trim();
        if (StateMap.TryGetValue(trimmed, out var match))
        {
            return match;
        }

        // Eğer 2 harfli büyük harf kod ise kodu o yap
        if (trimmed.Length == 2 && char.IsLetter(trimmed[0]) && char.IsLetter(trimmed[1]))
        {
            return (trimmed.ToUpperInvariant(), trimmed.ToUpperInvariant());
        }

        return (trimmed, trimmed);
    }

    /// <summary>
    /// Ülke kodunun ABD veya Kanada olup olmadığını döner (bu ülkelerde Eyalet zorunludur).
    /// </summary>
    public static bool RequiresState(string? countryCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode))
        {
            return false;
        }

        string c = countryCode.Trim().ToUpperInvariant();
        return c == "US" || c == "USA" || c == "CA" || c == "CAN";
    }
}
