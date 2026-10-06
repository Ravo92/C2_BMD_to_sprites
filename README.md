# C2 BMD to sprites

A .NET 10 Windows command-line converter for Cultures BMD sprite managers. Supports bob types 0 (unallocated record), 1 (palette indices), 2 (mask), 3 (time mask), and 4 (palette index plus blend byte).

## Commands

Run the compiled executable with `--help` for all arguments. No arguments print help; there are no hard-coded game paths or automatic writes to the installed game.

```powershell
C2_BMD_to_sprites.exe decode original.bmd frames
C2_BMD_to_sprites.exe encode original.bmd frames recreated.bmd
C2_BMD_to_sprites.exe compare original.bmd recreated.bmd
C2_BMD_to_sprites.exe probe original.bmd
```

Without a palette directory, decode and encode use `name_000.indices.png`. R stores the original palette index/time value, G stores the type-4 blend byte, B is zero, and A stores run occupancy (0 or 255). This distinguishes literal index zero, literal alpha zero, duplicate palette colors and skipped pixels. These PNGs are numeric interchange images, not colored previews.

For existing GUI palette rules, add the palette directory and, optionally, the logical BMD name:

```powershell
C2_BMD_to_sprites.exe decode ls_gui_window_original.bmd frames palettes ls_gui_window.bmd
C2_BMD_to_sprites.exe encode ls_gui_window_original.bmd frames recreated.bmd palettes ls_gui_window.bmd
# A dash selects raw PNGs while still allowing an explicit logical filename.
C2_BMD_to_sprites.exe encode ls_gui_window_original.bmd frames recreated-raw.bmd - ls_gui_window.bmd
```

GUI export creates both colored `name_000.png` previews and lossless index PNGs. GUI import reads the colored previews. Unchanged preview pixels retain the template's original palette indices; changed RGB values must match the selected palette exactly. Duplicate colors without an unchanged template hint select the lowest matching index. Type-1 and mask pixels must have binary alpha. No nearest-color quantization or alpha threshold silently changes the data. For precise index edits, use raw PNGs.

The inherited GUI palette ranges apply to `ls_gui_window.bmd`. Other names use the background palette unless rules are added; raw mode works independently of palettes. The preview alpha approximates type-4 blending on a transparent background; the engine uses blend arithmetic on the actual destination and normal drawing can ignore that byte. Time-mask previews display timing values as gray, not ordinary transparency.

Unallocated records are retained and do not produce artificial 1x1 PNGs. Active frame dimensions and signed offsets are preserved from the template. The current importer requires a template and unchanged frame dimensions; arbitrary new RGBA artwork still needs an explicit palette, anchor/bounding-box policy and frame IDs.

## Packing and comparison

`repack input.bmd output.bmd` decodes all sprite rows into values/occupancy and regenerates the packed stream and line table. It does not copy the original compressed buffers. The default reconstructs AddBob order from stored row indices; IDs and creation order can differ. `--id-order` deliberately generates in ID order for investigation.

`probe input.bmd` does the same work in memory and prints JSON with SHA-256, first differing byte, table/stream equality and decoded pixel/anchor equality. `compare` returns 0 for byte equality, 2 for a valid difference, and 1 for an error. `probe` reports differences in JSON and returns 0 after a successful comparison.

`rewrite` only reads and serializes existing buffers and metadata; byte equality in this mode proves serialization fidelity, not the packing algorithm.

`--optimize` enables the Mac generator's first-match byte-stream reuse and contiguous line-table reuse. This is an explicit candidate mode: the installed audit corpus contains no optimized originals. Optimized assets can lose the original creation-order history. A byte comparison is necessary before claiming that mode reproduces an original asset.

BMD output must differ from the input/template. Existing outputs require `--force`. PNG export refuses to overwrite existing frame files. Originals are read-only inputs.

## Confirmed results

On 2026-10-06, independent decode/repack matched **206 / 206 installed BMDs byte for byte** (133,294,533 bytes; 267,386 frame records, including nine empty managers). Types 0/1/2/3/4 are represented. Both colored-preview and raw-index PNG round trips matched the 193-frame GUI window BMD exactly. See [the audit](docs/BMD_ALGORITHM_AUDIT.md), [per-file hashes](docs/BMD_CORPUS_AUDIT.csv) and [format notes](docs/BMD_FORMAT.md).

This confirms the packing/serialization profile on that corpus with template metadata and recovered creation order. It does not prove how the original art tool quantized arbitrary source artwork or chose its initial anchors/crop rectangles.
