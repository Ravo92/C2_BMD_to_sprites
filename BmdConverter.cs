using System.Text.Json;
using C2_BMD_to_sprites.Decoders;
using C2_BMD_to_sprites.Encoder;

namespace C2_BMD_to_sprites;

internal static class BmdConverter
{
    internal static void Export(string inputPath, string outputDirectory, string? paletteSource, string logicalName, string? rawSubdirectory = null)
    {
        BmdFile file = BmdReader.Read(File.ReadAllBytes(inputPath));
        BmdProject project = BmdProject.FromFile(file, logicalName, paletteSource);
        if (rawSubdirectory is not null) foreach (BmdProjectFrame entry in project.Frames) if (entry.RawImage is not null) entry.RawImage = Path.Combine(rawSubdirectory, entry.RawImage);
        BmdPaletteSource? palettes = paletteSource is null ? null : new(paletteSource);
        string manifestPath = Path.Combine(outputDirectory, Path.GetFileNameWithoutExtension(logicalName) + ".frames.json");
        // Preflight all collisions and palette assignments before writing the first PNG.
        EnsureNewOutput(manifestPath);
        for (int i = 0; i < project.Frames.Length; i++)
        {
            BmdProjectFrame entry = project.Frames[i];
            if (entry.RawImage is null) continue;
            EnsureNewOutput(Path.Combine(outputDirectory, entry.RawImage));
            if (entry.PreviewImage is null) continue;
            EnsureNewOutput(Path.Combine(outputDirectory, entry.PreviewImage));
            palettes!.Resolve(logicalName, unchecked(file.Header.FirstBobId + (uint)i));
        }
        Directory.CreateDirectory(outputDirectory);
        if (rawSubdirectory is not null && project.Frames.Any(f => f.RawImage is not null)) Directory.CreateDirectory(Path.Combine(outputDirectory, rawSubdirectory));
        for (int i = 0; i < file.Frames.Length; i++)
        {
            BmdProjectFrame entry = project.Frames[i];
            if (entry.RawImage is null) continue; // Unallocated and zero-sized records live in the manifest.
            BmdFrame frame = file.Frames[i];
            BmdFramePixels decoded = BmdPixelDecoder.Decode(file, frame);
            RgbaImage raw = BmdPngCodec.Export(decoded, frame.Type, null);
            SavePng(raw, Path.Combine(outputDirectory, entry.RawImage));
            if (entry.PreviewImage is null) continue;
            Rgba32[] palette = palettes!.Resolve(logicalName, unchecked(file.Header.FirstBobId + (uint)i));
            RgbaImage preview = BmdPngCodec.Export(decoded, frame.Type, palette);
            SavePng(preview, Path.Combine(outputDirectory, entry.PreviewImage));
        }
        using FileStream manifest = new(manifestPath, FileMode.CreateNew, FileAccess.Write);
        JsonSerializer.Serialize(manifest, project, BmdProject.JsonOptions);
    }

    internal static byte[] Import(string templatePath, string framesDirectory, string? paletteSource, string logicalName, bool optimize, bool idOrder = false)
    {
        BmdFile template = BmdReader.Read(File.ReadAllBytes(templatePath));
        BmdPaletteSource? palettes = paletteSource is null ? null : new(paletteSource);
        BmdFramePixels LoadFrame(int frameIndex)
        {
            BmdFrame frame = template.Frames[frameIndex];
            if (frame.Width == 0 || frame.Height == 0) return new BmdFramePixels((int)frame.Width, (int)frame.Height);
            string suffix = palettes is null ? ".indices.png" : ".png";
            string path = Path.Combine(framesDirectory, BmdProject.FrameStem(logicalName, frameIndex) + suffix);
            if (palettes is null && !File.Exists(path)) path = Path.Combine(framesDirectory, "indices", BmdProject.FrameStem(logicalName, frameIndex) + suffix);
            Rgba32[]? palette = palettes?.Resolve(logicalName, unchecked(template.Header.FirstBobId + (uint)frameIndex));
            BmdFramePixels? original = palettes is null ? null : BmdPixelDecoder.Decode(template, frame);
            return BmdPngCodec.Import(path, frame, palette, original);
        }
        return BmdWriter.Write(BmdEncoder.Repack(template, optimize, LoadFrame, idOrder));
    }

    private static void SavePng(RgbaImage image, string path)
    {
        using FileStream stream = new(path, FileMode.CreateNew, FileAccess.Write);
        // Numeric index images must retain RGB even where occupancy/alpha is zero.
        image.SavePng(stream);
    }

    private static void EnsureNewOutput(string path)
    {
        if (File.Exists(path) || Directory.Exists(path)) throw new IOException($"Output already exists: {path}");
    }
}
