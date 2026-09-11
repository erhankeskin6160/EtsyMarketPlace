namespace EtsyMarketPlace.Infrastructure.ShopVault.ImageSanitization;

using EtsyMarketPlace.Domain.ShopVault.Interfaces;
using SkiaSharp;

public sealed class ExifStripperImageProcessor : IExifStripper
{
    public byte[] ProcessImage(byte[] inputBytes, bool stripExif = true, bool permutateHash = true)
    {
        if (inputBytes == null || inputBytes.Length == 0)
        {
            return Array.Empty<byte>();
        }

        using var originStream = new MemoryStream(inputBytes);
        using var codec = SKCodec.Create(originStream);
        if (codec == null)
        {
            return inputBytes;
        }

        var originFormat = codec.EncodedFormat;
        using var originalBitmap = SKBitmap.Decode(codec);
        if (originalBitmap == null)
        {
            return inputBytes;
        }

        SKBitmap workingBitmap = originalBitmap;
        bool workingBitmapOwned = false;

        try
        {
            if (permutateHash && originalBitmap.Width > 50 && originalBitmap.Height > 50)
            {
                // Micro-crop 1-2 pixels to alter perceptual hash (pHash) and spatial frequency footprint
                int cropX = 1;
                int cropY = 1;
                int newWidth = originalBitmap.Width - 2;
                int newHeight = originalBitmap.Height - 2;

                var subsetRect = new SKRectI(cropX, cropY, cropX + newWidth, cropY + newHeight);
                var croppedBitmap = new SKBitmap();
                if (originalBitmap.ExtractSubset(croppedBitmap, subsetRect))
                {
                    workingBitmap = croppedBitmap;
                    workingBitmapOwned = true;
                }
            }

            // Encode with clean metadata
            var format = originFormat == SKEncodedImageFormat.Png ? SKEncodedImageFormat.Png : SKEncodedImageFormat.Jpeg;
            int quality = format == SKEncodedImageFormat.Jpeg ? 93 : 100;

            using var image = SKImage.FromBitmap(workingBitmap);
            using var data = image.Encode(format, quality);
            return data.ToArray();
        }
        finally
        {
            if (workingBitmapOwned)
            {
                workingBitmap.Dispose();
            }
        }
    }
}
