using System.Buffers.Binary;
using System.IO.Compression;

namespace Dayswork.Tests.Integration;

/// <summary>
/// Just enough PNG to test the two authored paint masks without pulling in an image library.
///
/// Both invariants these tests check are silent when broken — the game's <c>BuildingPainter</c>
/// indexes a mask by flat pixel position and matches its colours by exact equality, so a resized or
/// slightly-off mask mispaints with no error anywhere (see <c>docs/game-data/painting.md</c>).
/// </summary>
internal static class PaintMaskAsset
{
    /// <summary>Reads width/height out of a PNG's IHDR chunk — the header is fixed-layout, so this
    /// needs no decoding at all.</summary>
    public static (int Width, int Height) ReadSize(string path)
    {
        var header = new byte[24];
        using var stream = File.OpenRead(path);
        if (stream.Read(header, 0, header.Length) != header.Length)
            throw new InvalidDataException($"'{path}' is too short to be a PNG.");

        if (header[0] != 0x89 || header[1] != 0x50 || header[2] != 0x4E || header[3] != 0x47)
            throw new InvalidDataException($"'{path}' is not a PNG.");

        return (
            BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(16, 4)),
            BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(20, 4)));
    }

    /// <summary>Every distinct RGBA colour in an 8-bit truecolour-with-alpha PNG.</summary>
    public static HashSet<(byte R, byte G, byte B, byte A)> DistinctColors(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var (width, height) = ReadSize(path);

        if (bytes[24] != 8 || bytes[25] != 6 || bytes[26] != 0 || bytes[27] != 0 || bytes[28] != 0)
            throw new InvalidDataException(
                $"'{path}' is not a non-interlaced 8-bit RGBA PNG; re-export it as one so this test can read it.");

        var pixels = Unfilter(Inflate(ConcatenateIdat(bytes)), width, height);

        var colors = new HashSet<(byte, byte, byte, byte)>();
        for (int i = 0; i < pixels.Length; i += 4)
            colors.Add((pixels[i], pixels[i + 1], pixels[i + 2], pixels[i + 3]));

        return colors;
    }

    private static byte[] ConcatenateIdat(byte[] png)
    {
        var idat = new MemoryStream();
        int offset = 8; // signature
        while (offset + 8 <= png.Length)
        {
            int length = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(offset, 4));
            string type = System.Text.Encoding.ASCII.GetString(png, offset + 4, 4);
            if (type == "IDAT")
                idat.Write(png, offset + 8, length);
            else if (type == "IEND")
                break;

            offset += 12 + length; // length + type + data + CRC
        }

        return idat.ToArray();
    }

    private static byte[] Inflate(byte[] zlib)
    {
        using var source = new MemoryStream(zlib);
        using var inflater = new ZLibStream(source, CompressionMode.Decompress);
        using var inflated = new MemoryStream();
        inflater.CopyTo(inflated);
        return inflated.ToArray();
    }

    /// <summary>Reverses the five PNG scanline filters, in place, into a flat RGBA array.</summary>
    private static byte[] Unfilter(byte[] raw, int width, int height)
    {
        const int bpp = 4;
        int stride = width * bpp;
        var pixels = new byte[stride * height];

        for (int y = 0; y < height; y++)
        {
            int filter = raw[y * (stride + 1)];
            int from = y * (stride + 1) + 1;
            int to = y * stride;

            for (int x = 0; x < stride; x++)
            {
                byte left = x >= bpp ? pixels[to + x - bpp] : (byte)0;
                byte up = y > 0 ? pixels[to - stride + x] : (byte)0;
                byte upLeft = y > 0 && x >= bpp ? pixels[to - stride + x - bpp] : (byte)0;

                int value = filter switch
                {
                    0 => raw[from + x],
                    1 => raw[from + x] + left,
                    2 => raw[from + x] + up,
                    3 => raw[from + x] + ((left + up) / 2),
                    4 => raw[from + x] + Paeth(left, up, upLeft),
                    _ => throw new InvalidDataException($"Unknown PNG filter {filter} on row {y}."),
                };

                pixels[to + x] = (byte)value;
            }
        }

        return pixels;
    }

    private static byte Paeth(byte a, byte b, byte c)
    {
        int p = a + b - c;
        int pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }
}
