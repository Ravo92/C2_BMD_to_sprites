using System.Text.Json;
using System.Text.Json.Serialization;
using C2_BMD_to_sprites.Encoder;

namespace C2_BMD_to_sprites;

// Editable metadata for cropped frames. Array order defines bob IDs; FirstRowIndex
// retains generation order when packing an unmodified export without optimization.
internal sealed class BmdProject
{
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        IncludeFields = false
    };

    [JsonRequired] public int FormatVersion { get; set; } = 1;
    [JsonRequired] public string LogicalName { get; set; } = "sprites.bmd";
    public string? PaletteSource { get; set; }
    public uint Version { get; set; }
    public uint FirstBobId { get; set; }
    public uint[] SectionVersions { get; set; } = [0, 0, 0];
    public byte[] TrailingData { get; set; } = [];
    [JsonRequired] public BmdProjectFrame[] Frames { get; set; } = [];

    internal static BmdProject FromFile(BmdFile file, string logicalName, string? paletteSource)
    {
        return new BmdProject
        {
            LogicalName = logicalName,
            PaletteSource = paletteSource is null ? null : Path.GetFullPath(paletteSource),
            Version = file.Header.Version,
            FirstBobId = file.Header.FirstBobId,
            SectionVersions = file.SectionVersions,
            TrailingData = file.TrailingData,
            Frames = file.Frames.Select((f, i) => new BmdProjectFrame
            {
                Type = f.Type, XOffset = f.XOffset, YOffset = f.YOffset, Width = f.Width, Height = f.Height, FirstRowIndex = f.FirstRowIndex,
                RawImage = f.Type == 0 || f.Width == 0 || f.Height == 0 ? null : $"{FrameStem(logicalName, i)}.indices.png",
                PreviewImage = paletteSource is null || f.Type == 0 || f.Width == 0 || f.Height == 0 ? null : $"{FrameStem(logicalName, i)}.png"
            }).ToArray()
        };
    }

    internal static byte[] Build(string manifestPath, string? paletteSource, bool preview, bool optimize, bool idOrder)
    {
        BmdProject project = JsonSerializer.Deserialize<BmdProject>(File.ReadAllText(manifestPath), JsonOptions) ?? throw new InvalidDataException("Empty frame manifest.");
        if (project.FormatVersion != 1) throw new InvalidDataException($"Unsupported frame manifest version {project.FormatVersion}.");
        if (string.IsNullOrWhiteSpace(project.LogicalName) || project.Frames is null || project.Frames.Length > 1_000_000 || project.Frames.Any(f => f is null) || project.TrailingData is null || project.SectionVersions is null || (project.Frames.Length != 0 && project.SectionVersions.Length != 3)) throw new InvalidDataException("Invalid frame manifest metadata.");
        string directory = Path.GetDirectoryName(Path.GetFullPath(manifestPath))!;
        string? selectedPalette = paletteSource ?? project.PaletteSource;
        if (preview && selectedPalette is null) throw new InvalidDataException("Preview import requires a PCX palette, GUI palette directory or JSON palette map.");
        if (!preview && paletteSource is not null) throw new ArgumentException("A palette source for build requires --preview; raw images contain exact indices.");
        BmdPaletteSource? palettes = preview ? new BmdPaletteSource(Path.GetFullPath(selectedPalette!, paletteSource is null ? directory : Environment.CurrentDirectory)) : null;
        BmdFrame[] frames = project.Frames.Select(f => new BmdFrame(f.Type, f.XOffset, f.YOffset, f.Width, f.Height, f.FirstRowIndex)).ToArray();
        foreach (BmdFrame frame in frames) if (frame.Type != 0)
        {
            if (frame.Type > 4) throw new InvalidDataException($"Unsupported bob type {frame.Type} in manifest.");
            BmdFramePixels.ValidateDimensions(frame.Width, frame.Height);
        }
        BmdFile source = new(new BmdHeader(project.Version, project.FirstBobId, (uint)frames.Length, 0, 0, 0, 0, 0), frames, [], [], project.SectionVersions, project.TrailingData);
        BmdFramePixels LoadFrame(int i)
        {
            BmdFrame frame = frames[i];
            if (frame.Width == 0 || frame.Height == 0) return new BmdFramePixels((int)frame.Width, (int)frame.Height);
            BmdProjectFrame entry = project.Frames[i];
            string? image = preview ? entry.PreviewImage : entry.RawImage;
            if (string.IsNullOrWhiteSpace(image)) throw new InvalidDataException($"Frame {i} has no {(preview ? "preview" : "raw")} image path.");
            Rgba32[]? palette = palettes?.Resolve(project.LogicalName, unchecked(project.FirstBobId + (uint)i));
            // Raw export supplies exact hints for duplicate indices and alpha-zero literals.
            BmdFramePixels? original = preview && !string.IsNullOrWhiteSpace(entry.RawImage) ? BmdPngCodec.Import(Path.GetFullPath(entry.RawImage, directory), frame, null) : null;
            return BmdPngCodec.Import(Path.GetFullPath(image, directory), frame, palette, original);
        }
        byte[] bytes = BmdWriter.Write(BmdEncoder.Repack(source, optimize, LoadFrame, idOrder));
        BmdReader.Read(bytes);
        return bytes;
    }

    internal static string FrameStem(string logicalName, int index) => $"{Path.GetFileNameWithoutExtension(logicalName)}_{index:D3}";
}

internal sealed class BmdProjectFrame
{
    [JsonRequired] public uint Type { get; set; }
    public int XOffset { get; set; }
    public int YOffset { get; set; }
    [JsonRequired] public uint Width { get; set; }
    [JsonRequired] public uint Height { get; set; }
    public uint FirstRowIndex { get; set; }
    public string? RawImage { get; set; }
    public string? PreviewImage { get; set; }
}
