namespace EtsyMarketPlace.Infrastructure.Shipping;

using EtsyMarketPlace.Application.Shipping;

/// <summary>
/// Kargo kimlik bilgileri ve oturum tokenleri için Windows DPAPI (CurrentUser) tabanlı koruyucu.
/// Aynı mekanizma <see cref="ShippingCredentialEncryptor"/> üzerinden yürür; yani değerler
/// yalnızca ilgili Windows kullanıcısında çözülebilir.
/// </summary>
public sealed class DpapiShippingSecretProtector : IShippingSecretProtector
{
    public string Protect(string? plainText) => ShippingCredentialEncryptor.Encrypt(plainText);

    public string Unprotect(string? cipherText) => ShippingCredentialEncryptor.Decrypt(cipherText);
}
