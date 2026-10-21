namespace C2_BMD_to_sprites.Decoders;

internal static class BmdPixelDecoder
{
    internal static BmdFramePixels Decode(BmdFile file, BmdFrame frame)
    {
        ValidateFrame(file, frame);
        if (frame.Type == 0) return new BmdFramePixels(0, 0);
        BmdFramePixels result = new(checked((int)frame.Width), checked((int)frame.Height));
        ProcessRows(file, frame, result);
        return result;
    }

    internal static void Validate(BmdFile file, BmdFrame frame)
    {
        ValidateFrame(file, frame);
        ProcessRows(file, frame, null);
    }

    private static void ValidateFrame(BmdFile file, BmdFrame frame)
    {
        if (frame.Type > 4) throw new NotSupportedException($"Unsupported bob type {frame.Type}; no guessed decoder is used.");
        if (frame.Type == 0) return; // Unallocated records can contain unused metadata.
        BmdFramePixels.ValidateDimensions(frame.Width, frame.Height);
        if ((ulong)frame.FirstRowIndex + frame.Height > (ulong)file.Rows.Length) throw new InvalidDataException("Frame row index range is outside the line table.");
    }

    private static void ProcessRows(BmdFile file, BmdFrame frame, BmdFramePixels? result)
    {
        if (frame.Type == 0) return;
        int width = checked((int)frame.Width);
        for (int y = 0; y < frame.Height; y++)
        {
            BmdRow row = file.Rows[checked((int)frame.FirstRowIndex + y)];
            if (row.IsEmpty) continue;
            int x = checked((int)row.Indent);
            if (x >= width) throw new InvalidDataException($"Row indentation exceeds width at row {y}.");
            int cursor = checked((int)row.PixelStreamOffset);
            if (cursor >= file.Pixels.Length) throw new InvalidDataException($"Row {y} points outside the pixel stream.");
            bool ended = false;
            while (cursor < file.Pixels.Length)
            {
                byte control = file.Pixels[cursor++];
                if (control == 0) { ended = true; break; }
                int length = frame.Type == 3 ? control : control & 127;
                if (length == 0 || length > width - x) throw new InvalidDataException($"Invalid run length at row {y}, x {x}.");
                if (frame.Type != 3 && (control & 128) != 0) { x += length; continue; }
                int payload = frame.Type switch { 2 => 0, 3 => 1, 4 => length * 2, _ => length };
                if (payload > file.Pixels.Length - cursor) throw new EndOfStreamException($"Truncated row payload at row {y}.");
                if (result is null) { x += length; cursor += payload; continue; }
                for (int k = 0; k < length; k++)
                {
                    ushort value = frame.Type switch { 2 => 255, 3 => file.Pixels[cursor], 4 => (ushort)(file.Pixels[cursor + k * 2] | file.Pixels[cursor + k * 2 + 1] << 8), _ => file.Pixels[cursor + k] };
                    int index = y * result.Width + x++;
                    if (frame.Type == 3 && value == 255) continue;
                    result.Values[index] = value;
                    result.Occupied[index] = true;
                }
                cursor += payload;
            }
            if (!ended) throw new EndOfStreamException($"Missing row terminator at row {y}.");
        }
    }
}
