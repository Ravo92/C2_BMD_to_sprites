namespace C2_BMD_to_sprites.Encoder;

internal static class BmdRowEncoder
{
    internal static byte[] Encode(uint type, ReadOnlySpan<ushort> values, ReadOnlySpan<bool> occupied, out uint indent)
    {
        if (values.Length != occupied.Length) throw new ArgumentException("Pixel and occupancy lengths differ.");
        if (type is not (1 or 2 or 3 or 4)) throw new NotSupportedException($"Cannot encode bob type {type}.");
        int first = 0;
        while (first < occupied.Length && !occupied[first]) first++;
        indent = checked((uint)first);
        if (first == occupied.Length) { indent = 0; return []; }
        if (first > 1023) throw new InvalidDataException("Row indentation exceeds ten bits.");
        int end = occupied.Length;
        while (end > first && !occupied[end - 1]) end--;
        List<byte> output = [];
        int x = first;
        while (x < end)
        {
            bool visible = occupied[x];
            int maxLength = type == 3 ? 255 : 127;
            ushort value = visible ? values[x] : (ushort)255;
            int length = 1;
            while (length < maxLength && x + length < end && occupied[x + length] == visible && (type != 3 || !visible || values[x + length] == value)) length++;
            if (type == 3)
            {
                if (value > 255 || (visible && value == 255)) throw new InvalidDataException("Time-mask values must be 0..254; 255 denotes skipped pixels.");
                output.Add((byte)length);
                output.Add((byte)value);
            }
            else
            {
                output.Add((byte)(length | (visible ? 0 : 128)));
                if (visible && type != 2)
                {
                    for (int i = 0; i < length; i++)
                    {
                        ushort pixel = values[x + i];
                        if (type == 1 && pixel > 255) throw new InvalidDataException("Palette index exceeds one byte.");
                        output.Add((byte)pixel);
                        if (type == 4) output.Add((byte)(pixel >> 8));
                    }
                }
            }
            x += length;
        }
        // Mac PackLine omits trailing transparent runs, then writes exactly one terminator.
        output.Add(0);
        return output.ToArray();
    }
}
