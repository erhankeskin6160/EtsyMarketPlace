namespace EtsyMarketPlace.Application.Shipping;

/// <summary>
/// Hassas değerlerin (oturum tokeni, şifre) diskte şifrelenmesi için soyutlama.
/// Uygulama katmanı yalnızca bu arayüzü bilir; somut şifreleme altyapı katmanındadır.
/// </summary>
public interface IShippingSecretProtector
{
    string Protect(string? plainText);
    string Unprotect(string? cipherText);
}

/// <summary>
/// Şifreleyicinin uygulama genelindeki kaydı. Composition root (Program.cs) bir kez doldurur.
/// Doldurulmazsa depo eski davranışını sürdürür (düz metin) ve korumalı bir dosya
/// okunamayacağı için token güvenli tarafta boşaltılır — asla şifreli metin token sanılmaz.
/// </summary>
public static class ShippingSecretProtector
{
    public static IShippingSecretProtector? Current { get; set; }
}
