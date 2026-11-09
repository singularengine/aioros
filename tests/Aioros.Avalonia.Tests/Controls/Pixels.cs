namespace Aioros.Avalonia.Tests.Controls;

internal static class Pixels
{
    public static int Red(byte[] png)
    {
        var (bytes, _, bgra) = Decode(png);
        var red = 0;
        for (var index = 0; index < bytes.Length; index += 4)
        {
            var (r, g, b) = bgra ? (bytes[index + 2], bytes[index + 1], bytes[index]) : (bytes[index], bytes[index + 1], bytes[index + 2]);
            if (r > 200 && g < 80 && b < 80)
            {
                red++;
            }
        }

        return red;
    }

    public static (byte R, byte G, byte B) At(byte[] png, int x, int y)
    {
        var (bytes, width, bgra) = Decode(png);
        var index = ((y * width) + x) * 4;
        return bgra ? (bytes[index + 2], bytes[index + 1], bytes[index]) : (bytes[index], bytes[index + 1], bytes[index + 2]);
    }

    private static (byte[] Bytes, int Width, bool Bgra) Decode(byte[] png)
    {
        using var stream = new MemoryStream(png);
        using var bitmap = new global::Avalonia.Media.Imaging.Bitmap(stream);
        var stride = bitmap.PixelSize.Width * 4;
        var bytes = new byte[stride * bitmap.PixelSize.Height];
        var handle = System.Runtime.InteropServices.GCHandle.Alloc(bytes, System.Runtime.InteropServices.GCHandleType.Pinned);
        try
        {
            bitmap.CopyPixels(new global::Avalonia.PixelRect(bitmap.PixelSize), handle.AddrOfPinnedObject(), bytes.Length, stride);
        }
        finally
        {
            handle.Free();
        }

        return (bytes, bitmap.PixelSize.Width, bitmap.Format == global::Avalonia.Platform.PixelFormat.Bgra8888);
    }
}
