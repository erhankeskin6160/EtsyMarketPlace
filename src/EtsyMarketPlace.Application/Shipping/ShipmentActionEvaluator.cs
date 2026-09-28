namespace EtsyMarketPlace.Application.Shipping;

using System;

/// <summary>
/// Eylem çubuğunun durumu: buton basılabilir mi, üzerinde ne yazmalı, kullanıcıya ne anlatılmalı.
/// </summary>
public sealed record ShipmentActionState(
    bool CanCreate,
    string ButtonText,
    string StatusMessage,
    string Tone);

/// <summary>
/// "Gönderi Oluştur" butonunun durumunu belirleyen kural.
///
/// Neden Application katmanında: bu karar arayüzün içinde gömülüydü ve iki kez yanlış
/// sonuç verdi (teklif seçili değilken buton aktif görünüyordu). Kurallar artık tek yerde
/// ve testli; arayüz yalnızca sonucu uygular.
/// </summary>
public static class ShipmentActionEvaluator
{
    public const string ToneNeutral = "neutral";
    public const string ToneSuccess = "success";
    public const string ToneWarning = "warning";

    public static ShipmentActionState Evaluate(
        bool hasSelectedQuote,
        bool creationSupported,
        string? providerName,
        QuoteSource quoteSource)
    {
        if (!hasSelectedQuote)
        {
            return new ShipmentActionState(
                CanCreate: false,
                ButtonText: "Gönderi Oluştur",
                StatusMessage: "Önce sipariş seçip geçerli paket ölçüleri girin.",
                Tone: ToneNeutral);
        }

        string provider = string.IsNullOrWhiteSpace(providerName) ? "Taşıyıcı" : providerName!;

        if (!creationSupported)
        {
            return new ShipmentActionState(
                CanCreate: false,
                ButtonText: "Gönderi kapalı",
                StatusMessage: $"{provider} için gönderi oluşturma henüz aktif değil; yalnız karşılaştırma amaçlıdır.",
                Tone: ToneWarning);
        }

        string liveNote = quoteSource == QuoteSource.Live
            ? "Canlı teklif seçildi."
            : "Tahmini tarife seçildi — fiyat teyidi önerilir.";

        return new ShipmentActionState(
            CanCreate: true,
            ButtonText: $"{provider} ile Gönderi Oluştur",
            StatusMessage: liveNote,
            Tone: quoteSource == QuoteSource.Live ? ToneSuccess : ToneWarning);
    }
}
