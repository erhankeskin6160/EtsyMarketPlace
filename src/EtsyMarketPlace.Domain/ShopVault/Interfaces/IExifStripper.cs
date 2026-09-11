namespace EtsyMarketPlace.Domain.ShopVault.Interfaces;

public interface IExifStripper
{
    byte[] ProcessImage(byte[] inputBytes, bool stripExif = true, bool permutateHash = true);
}
