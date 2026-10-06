using System.Security.Cryptography;
using C2_BMD_to_sprites.Decoders;

namespace C2_BMD_to_sprites;

internal static class BmdComparison
{
    internal static object Describe(byte[] originalBytes, byte[] candidateBytes)
    {
        BmdFile original = BmdReader.Read(originalBytes);
        BmdFile candidate = BmdReader.Read(candidateBytes);
        int first = -1, differing = Math.Abs(originalBytes.Length - candidateBytes.Length);
        for (int i = 0; i < Math.Min(originalBytes.Length, candidateBytes.Length); i++)
        {
            if (originalBytes[i] == candidateBytes[i]) continue;
            if (first < 0) first = i;
            differing++;
        }
        if (first < 0 && originalBytes.Length != candidateBytes.Length) first = Math.Min(originalBytes.Length, candidateBytes.Length);
        bool pixelsEqual = original.Frames.Length == candidate.Frames.Length;
        int firstDifferentFrame = -1;
        for (int i = 0; pixelsEqual && i < original.Frames.Length; i++)
        {
            BmdFrame a = original.Frames[i], b = candidate.Frames[i];
            pixelsEqual = a.Type == b.Type && a.XOffset == b.XOffset && a.YOffset == b.YOffset && a.Width == b.Width && a.Height == b.Height;
            if (pixelsEqual && a.Type != 0)
            {
                BmdFramePixels left = BmdPixelDecoder.Decode(original, a), right = BmdPixelDecoder.Decode(candidate, b);
                pixelsEqual = left.Values.AsSpan().SequenceEqual(right.Values) && left.Occupied.AsSpan().SequenceEqual(right.Occupied);
            }
            if (!pixelsEqual) firstDifferentFrame = i;
        }
        return new
        {
            ByteIdentical = originalBytes.AsSpan().SequenceEqual(candidateBytes),
            SpritePixelsAndAnchorsEqual = pixelsEqual && original.Header.FirstBobId == candidate.Header.FirstBobId,
            FirstDifferentByte = first < 0 ? (int?)null : first,
            DifferingBytesAtSameOffsets = differing,
            FirstDifferentFrame = firstDifferentFrame < 0 ? (int?)null : firstDifferentFrame,
            Original = Summary(original, originalBytes),
            Candidate = Summary(candidate, candidateBytes),
            PixelStreamIdentical = original.Pixels.AsSpan().SequenceEqual(candidate.Pixels),
            RowTableIdentical = original.Rows.AsSpan().SequenceEqual(candidate.Rows),
            FrameTableIdentical = original.Frames.AsSpan().SequenceEqual(candidate.Frames)
        };
    }

    private static object Summary(BmdFile file, byte[] bytes) => new { Bytes = bytes.Length, Sha256 = Convert.ToHexString(SHA256.HashData(bytes)), file.Header, Types = file.Frames.GroupBy(f => f.Type).ToDictionary(g => g.Key.ToString(), g => g.Count()), TrailingBytes = file.TrailingData.Length };
}
