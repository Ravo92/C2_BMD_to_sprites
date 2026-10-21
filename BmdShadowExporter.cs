using C2_BMD_to_sprites.Decoders;

namespace C2_BMD_to_sprites;

internal static class BmdShadowExporter
{
    internal static void ExportAdjacent(string inputPath, string outputDirectory)
    {
        string stem = Path.GetFileNameWithoutExtension(inputPath);
        if (stem.EndsWith("_s", StringComparison.OrdinalIgnoreCase))
        {
            ExportMasks(BmdReader.Read(File.ReadAllBytes(inputPath)), stem, outputDirectory);
            return;
        }
        string path = Path.Combine(Path.GetDirectoryName(inputPath)!, stem + "_s" + Path.GetExtension(inputPath));
        if (!File.Exists(path)) return;
        BmdFile shadows = BmdReader.Read(File.ReadAllBytes(path));
        ValidateMaskTypes(shadows);
        string directory = Path.Combine(outputDirectory, "shadows");
        BmdConverter.Export(path, directory, null, Path.GetFileName(path), "indices");
        ExportMasks(shadows, Path.GetFileNameWithoutExtension(path), directory);
        Console.WriteLine($"Shadow masks exported separately to: {directory}");
    }

    private static void ExportMasks(BmdFile file, string name, string directory)
    {
        ValidateMaskTypes(file);
        for (int i = 0; i < file.Frames.Length; i++)
        {
            BmdFrame frame = file.Frames[i];
            if (frame.Type == 0 || frame.Width == 0 || frame.Height == 0) continue;
            BmdFramePixels pixels = BmdPixelDecoder.Decode(file, frame);
            // This is an opaque coverage mask, not a guessed shadow opacity.
            // Runtime shadows darken the existing destination pixels.
            RgbaImage mask = BmdPngCodec.Export(pixels, 2, []);
            using FileStream stream = new(Path.Combine(directory, BmdProject.FrameStem(name, i) + ".mask.png"), FileMode.CreateNew, FileAccess.Write);
            mask.SavePng(stream);
        }
    }

    private static void ValidateMaskTypes(BmdFile file)
    {
        if (file.Frames.Any(f => f.Type is not (0 or 2))) throw new InvalidDataException("Expected type-2 shadow masks; the _s file was not combined with the artwork.");
    }
}
