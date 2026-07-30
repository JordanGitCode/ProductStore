using SkiaSharp;

namespace ProductStoreAPI.Services;

// Phone photos are far larger than a vision model needs and are often stored
// rotated with an EXIF origin. Both matter: full-resolution images overflow the
// model's context and take minutes to process on CPU, and an unrotated image
// costs the model any text in the photo.
public static class ScanImageResizer
{
    public static byte[] ToJpeg(byte[] source, int maxEdge, int quality)
    {
        using var codec = SKCodec.Create(new MemoryStream(source));
        if (codec is null)
        {
            throw new InvalidOperationException("Image could not be decoded.");
        }

        using var decoded = SKBitmap.Decode(codec);
        using var upright = ApplyOrigin(decoded, codec.EncodedOrigin);

        var scale = Math.Min(1.0f, (float)maxEdge / Math.Max(upright.Width, upright.Height));
        var width = Math.Max(1, (int)(upright.Width * scale));
        var height = Math.Max(1, (int)(upright.Height * scale));

        // Mitchell cubic, not the default nearest-neighbour: a ~5x downscale with
        // nearest-neighbour aliases away small text, which is exactly the detail
        // the scan depends on.
        using var resized = upright.Resize(
            new SKImageInfo(width, height), new SKSamplingOptions(SKCubicResampler.Mitchell));
        using var image = SKImage.FromBitmap(resized);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, quality);
        var bytes = data.ToArray();
        if (Environment.GetEnvironmentVariable("SCAN_DUMP_DIR") is { } dump)
        {
            File.WriteAllBytes(Path.Combine(dump, $"scan_{Guid.NewGuid():N}.jpg"), bytes);
        }
        return bytes;
    }

    private static SKBitmap ApplyOrigin(SKBitmap bitmap, SKEncodedOrigin origin)
    {
        if (origin == SKEncodedOrigin.TopLeft)
        {
            return bitmap.Copy();
        }

        var quarterTurned = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var width = quarterTurned ? bitmap.Height : bitmap.Width;
        var height = quarterTurned ? bitmap.Width : bitmap.Height;

        var result = new SKBitmap(width, height);
        using var canvas = new SKCanvas(result);

        switch (origin)
        {
            case SKEncodedOrigin.TopRight:
                canvas.Translate(width, 0);
                canvas.Scale(-1, 1);
                break;
            case SKEncodedOrigin.BottomRight:
                canvas.Translate(width, height);
                canvas.RotateDegrees(180);
                break;
            case SKEncodedOrigin.BottomLeft:
                canvas.Translate(0, height);
                canvas.Scale(1, -1);
                break;
            case SKEncodedOrigin.LeftTop:
                canvas.RotateDegrees(90);
                canvas.Scale(1, -1);
                break;
            case SKEncodedOrigin.RightTop:
                canvas.Translate(width, 0);
                canvas.RotateDegrees(90);
                break;
            case SKEncodedOrigin.RightBottom:
                canvas.Translate(width, 0);
                canvas.RotateDegrees(90);
                canvas.Translate(height, 0);
                canvas.Scale(-1, 1);
                break;
            case SKEncodedOrigin.LeftBottom:
                canvas.Translate(0, height);
                canvas.RotateDegrees(270);
                break;
        }

        canvas.DrawBitmap(bitmap, 0, 0, SKSamplingOptions.Default);
        return result;
    }
}
