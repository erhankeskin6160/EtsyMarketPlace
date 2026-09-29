namespace EtsyMarketPlace.Application.Shipping;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Domain.Shipping;

/// <summary>
/// Ship to More gönderilerinde GTİP/HS kodunu API'nin kabul ettiği forma çözer.
/// API kuralı: <c>hs_code</c>, "GET /v1/hs-codes" aramasında dönen bir kodla birebir
/// eşleşmelidir (ayraçlar yok sayılır). TR GTİP kodları 10-12 hane olabildiği hâlde
/// API 8 haneli kodlar tutar; bu yüzden kod sırayla tam hâli, ilk 8 hanesi ve ilk
/// 6 hanesi ile aranır ve yalnızca API'nin döndürdüğü kod kullanılır.
/// </summary>
public static class ShiptomoreHsCodeResolver
{
    /// <summary>Çözümleme sonucu: başarılıysa API'nin kabul ettiği kod.</summary>
    public sealed class Resolution
    {
        private Resolution(bool isResolved, string code, string errorMessage)
        {
            IsResolved = isResolved;
            Code = code;
            ErrorMessage = errorMessage;
        }

        public bool IsResolved { get; }

        public string Code { get; }

        public string ErrorMessage { get; }

        public static Resolution Resolved(string code) => new(true, code, string.Empty);

        public static Resolution Failed(string errorMessage) => new(false, string.Empty, errorMessage);
    }

    /// <summary>
    /// Kodu API'nin kod listesine çözer. Eşleşme yoksa kullanıcıya gösterilecek
    /// Türkçe hata mesajıyla döner.
    /// </summary>
    public static async Task<Resolution> ResolveAsync(
        string rawCode,
        IShiptomoreOfficialApi api,
        CancellationToken cancellationToken = default)
    {
        string digits = KeepDigits(rawCode);
        if (digits.Length < 2)
        {
            return Resolution.Failed(
                $"Ship to More için GTİP kodu geçersiz: \"{(rawCode ?? string.Empty).Trim()}\". En az 2 haneli bir kod girin.");
        }

        IReadOnlyList<ShiptomoreHsCode> lastResults = Array.Empty<ShiptomoreHsCode>();

        foreach (string candidate in BuildCandidates(digits))
        {
            var results = await api.SearchHsCodesAsync(candidate, 50, cancellationToken).ConfigureAwait(false);
            lastResults = results;

            foreach (var item in results)
            {
                if (KeepDigits(item.Code) == candidate)
                {
                    return Resolution.Resolved(item.Code);
                }
            }
        }

        var suggestions = lastResults
            .Select(r => r.Code)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Take(3)
            .ToList();

        string hint = suggestions.Count > 0
            ? " Ship to More'daki benzer kodlar: " + string.Join(", ", suggestions) + "."
            : string.Empty;

        return Resolution.Failed(
            $"Ship to More bu GTİP kodunu tanımıyor: {digits}. Lütfen geçerli bir GTİP/HS kodu seçip yeniden deneyin.{hint}");
    }

    /// <summary>Aranacak adaylar: tam kod, ilk 8 hane ve ilk 6 hane (yalnızca gerektiğinde).</summary>
    public static IReadOnlyList<string> BuildCandidates(string digits)
    {
        var candidates = new List<string> { digits };

        if (digits.Length > 8)
        {
            candidates.Add(digits.Substring(0, 8));
        }

        if (digits.Length > 6)
        {
            candidates.Add(digits.Substring(0, 6));
        }

        return candidates;
    }

    /// <summary>Ayraçları (nokta, boşluk, tire) yok sayar; yalnızca rakamları bırakır.</summary>
    public static string KeepDigits(string? code)
        => new string((code ?? string.Empty).Where(char.IsDigit).ToArray());
}
