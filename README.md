# C2 BMD to sprites

A .NET 10 Windows command-line converter for Cultures BMD sprite managers. Supports bob types 0 (unallocated record), 1 (palette indices), 2 (mask), 3 (time mask), and 4 (palette index plus blend byte).

## Drag and drop

Drag a single `.bmd` file onto `C2_BMD_to_sprites.exe`. It immediately exports the frames as PNGs plus an editable manifest into `<name>_sprites` in the **current working directory**. An existing folder/file is preserved; subsequent exports use `<name>_sprites_2`, `_3`, and so on. The current working directory is inherited from Explorer, a shortcut or your terminal; it is not necessarily the BMD's directory or the EXE's directory. The program prints the full output path.

The equivalent command is `C2_BMD_to_sprites.exe "input.bmd"`. No output path or `decode` argument is needed.

For automatic colored previews, place `input.palettes.json` or `input.pcx` beside `input.bmd`; the JSON map takes precedence. Without a sidecar, the converter exports exact `*.indices.png` files. BMD files do not contain their RGB palette, so colored previews require that external information. Existing explicit `decode`/`encode` commands remain available.

## Commands

Run the compiled executable with `--help` for all arguments. No arguments print help; there are no hard-coded game paths or automatic writes to the installed game.

```powershell
C2_BMD_to_sprites.exe decode original.bmd frames
C2_BMD_to_sprites.exe encode original.bmd frames recreated.bmd
C2_BMD_to_sprites.exe build frames/original.frames.json recreated.bmd
C2_BMD_to_sprites.exe compare original.bmd recreated.bmd
C2_BMD_to_sprites.exe probe original.bmd
```

Without a palette directory, decode and encode use `name_000.indices.png`. R stores the original palette index/time value, G stores the type-4 blend byte, B is zero, and A stores run occupancy (0 or 255). This distinguishes literal index zero, literal alpha zero, duplicate palette colors and skipped pixels. These PNGs are numeric interchange images, not colored previews.

For colored previews, supply a single PCX, a JSON palette map, or the existing GUI palette directory. The optional logical BMD name selects legacy GUI rules:

```powershell
C2_BMD_to_sprites.exe decode ls_gui_window_original.bmd frames palettes ls_gui_window.bmd
C2_BMD_to_sprites.exe encode ls_gui_window_original.bmd frames recreated.bmd palettes ls_gui_window.bmd
# A dash selects raw PNGs while still allowing an explicit logical filename.
C2_BMD_to_sprites.exe encode ls_gui_window_original.bmd frames recreated-raw.bmd - ls_gui_window.bmd
```

Colored export creates both `name_000.png` previews and lossless index PNGs. Template import with a palette source reads the colored previews. Unchanged preview pixels retain the template's original palette indices; alpha-only edits also retain duplicate indices. Changed RGB values must match the selected palette exactly. Duplicate colors without an original index hint select the lowest matching index. Type-1 and mask pixels must have binary alpha. No nearest-color quantization or alpha threshold silently changes the data. For precise index edits, use raw PNGs.

The inherited GUI palette ranges apply to `ls_gui_window.bmd`. Unknown names and uncovered bob IDs now produce an error instead of silently using the background palette. Raw mode works independently of palettes. The preview alpha approximates type-4 blending on a transparent background; the engine uses blend arithmetic on the actual destination and normal drawing can ignore that byte. Time-mask previews display timing values as gray, not ordinary transparency.

Unallocated and zero-sized records are retained without artificial 1x1 PNGs. Template import preserves frame dimensions and signed offsets. Use the editable manifest workflow below to change dimensions, offsets, types or frame count, or create a manager without a template. Arbitrary new RGBA artwork still needs an explicit palette and prepared frame bounds; automatic quantization/cropping is not implemented.

## Explicit palettes

A single PCX applies to all frames:

```powershell
C2_BMD_to_sprites.exe decode sprites.bmd frames creatures.pcx
```

A BMD can contain frames for multiple creatures or materials. Palettes are external game data; a BMD filename does not identify one universal palette. A JSON map assigns palettes using **absolute bob IDs** (`FirstBobId + frame index`, unsigned 32-bit). Paths are relative to the map file:

```json
{
  "Default": "palettes/default.pcx",
  "Ranges": [
    { "FirstBobId": 100, "LastBobId": 199, "Palette": "palettes/alternate.pcx" }
  ]
}
```

Ranges are inclusive and must not overlap. Omit `Default` to require a range for every exported/imported frame. This example is a schema illustration, not a game palette assignment.

```powershell
C2_BMD_to_sprites.exe decode sprites.bmd frames palettes.json
C2_BMD_to_sprites.exe encode sprites.bmd frames edited.bmd palettes.json
```

## Editable frame manifest

Every decode writes `name.frames.json`. The frame array defines bob IDs, including type-zero gaps. Each entry records type, dimensions, signed placement offsets, generation-order hint (`FirstRowIndex`) and image paths. Manager/section versions and trailing bytes are retained. Build regenerates packed buffers and generator counters; it does not copy compressed input data.

```powershell
# Exact raw index import; the original BMD is no longer required.
C2_BMD_to_sprites.exe build frames/sprites.frames.json edited.bmd
# Import colored previews using the saved palette source, with raw PNGs as index hints.
C2_BMD_to_sprites.exe build frames/sprites.frames.json edited.bmd --preview
# Override the saved palette source.
C2_BMD_to_sprites.exe build frames/sprites.frames.json edited.bmd palettes.json --preview
```

Image paths and a manually edited relative `PaletteSource` resolve from the manifest directory. A palette override on the command line resolves from the current directory. Decode records its palette source as an absolute path; edit it when moving projects between machines. Raw build does not load or require palettes.

For a new manager, create a manifest like this beside a prepared raw PNG:

```json
{
  "FormatVersion": 1,
  "LogicalName": "new.bmd",
  "FirstBobId": 0,
  "Frames": [
    { "Type": 4, "XOffset": -32, "YOffset": -56, "Width": 64, "Height": 64, "RawImage": "body.indices.png" }
  ]
}
```

New colored artwork can instead specify `PreviewImage` and `PaletteSource`, then use `--preview`. For a new frame with no original index hint, omit `RawImage`. If replacing/resizing existing artwork, update the referenced raw PNG too, or omit that hint for preview import. Type 2 occupies R=255; occupied type 3 values are R=0..254. Types 1 and 4 store palette index in R, and type 4 stores blend byte in G. All raw images use B=0 and A=0/255 for run occupancy. Input must be a single-frame PNG; raw values are not premultiplied or color-quantized.

## Packing and comparison

`repack input.bmd output.bmd` decodes all sprite rows into values/occupancy and regenerates the packed stream and line table. It does not copy the original compressed buffers. The default reconstructs AddBob order from stored row indices; IDs and creation order can differ. `--id-order` deliberately generates in ID order for investigation.

`probe input.bmd` does the same work in memory and prints JSON with SHA-256, first differing byte, table/stream equality and decoded pixel/anchor equality. `compare` returns 0 for byte equality, 2 for a valid difference, and 1 for an error. `probe` reports differences in JSON and returns 0 after a successful comparison.

`rewrite` only reads and serializes existing buffers and metadata; byte equality in this mode proves serialization fidelity, not the packing algorithm.

`--optimize` enables the Mac generator's first-match byte-stream reuse and contiguous line-table reuse. This is an explicit candidate mode: the installed audit corpus contains no optimized originals. Optimized assets can lose the original creation-order history. A byte comparison is necessary before claiming that mode reproduces an original asset.

BMD output must differ from the input/template/manifest. Existing outputs require `--force`. BMD writes use a temporary file followed by rename, preserving an existing output if validation or encoding fails. PNG export checks every planned filename and palette before writing, then creates files without overwriting. Disk/permission failures during export may leave a partial set of PNGs; the manifest is written last. Originals are read-only inputs.

## Confirmed results

On 2026-10-06, independent decode/repack matched **206 / 206 installed BMDs byte for byte** (133,294,533 bytes; 267,386 frame records, including nine empty managers). Types 0/1/2/3/4 are represented. Both colored-preview and raw-index PNG round trips matched the 193-frame GUI window BMD exactly. See [the audit](docs/BMD_ALGORITHM_AUDIT.md), [per-file hashes](docs/BMD_CORPUS_AUDIT.csv) and [format notes](docs/BMD_FORMAT.md).

This confirms the packing/serialization profile on that corpus with template metadata and recovered creation order. It does not prove how the original art tool quantized arbitrary source artwork or chose its initial anchors/crop rectangles.

On 2026-10-21, the refactored code regenerated **205 / 205 currently installed BMDs byte for byte** (132,357,458 bytes; 267,193 frame records). The historical extra 193-frame GUI copy is absent from this corpus. Raw-manifest, preview-manifest and colored-template PNG round trips of the installed GUI also match exactly. Twelve regression groups cover boundaries, malformed data, palettes and new-manager creation. See [the refactor verification](docs/REFACTOR_2026-10-21.md).

Build and run the independent regression checks:

```powershell
dotnet build C2_BMD_to_sprites.csproj -c Release
python tools/verify_codec.py bin/Release/net10.0-windows/C2_BMD_to_sprites.dll
# Optional read-only corpus and GUI PNG checks:
python tools/verify_codec.py bin/Release/net10.0-windows/C2_BMD_to_sprites.dll --corpus <game-data-dir> --gui-bmd <ls_gui_window.bmd> --gui-palettes <gui-palettes-dir>
```

PNG support uses **StbImageSharp 2.30.16** and **StbImageWriteSharp 1.16.7**, small C# ports of stb with no native binaries, registration or license keys. ImageSharp and its build-time license check have been removed. See [third-party notices](THIRD_PARTY_NOTICES.md) for upstream projects and licenses. Straight RGBA bytes preserve hidden RGB under alpha zero; there is no automatic color-profile conversion. The importer checks PNG dimensions/chunk boundaries and rejects APNG and duplicate headers before decoding.

After the dependency replacement, the standard Release build succeeds with **zero warnings and errors**. Fourteen regression groups pass, including drag-and-drop EXE invocation, output folder collisions, palette sidecars, indexed PNG/tRNS, 16-bit, Sub filtering, Adam7 interlacing and exact GUI PNG round trips.
