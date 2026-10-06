# BMD format and evidence

All integers are little-endian. Windows ASM and the current OpenVikings reader/writer/drawing functions establish the file and runtime layout. Mac decompiled generators supply original method names and the packing/optimization candidate. The user explicitly requested generator analysis for this separate converter; Windows still governs game reconstruction.

## Outer header (36 bytes)

| Offset | Field | Evidence |
| --- | --- | --- |
| 0 | Object ID 1012 (CBobManager) | Windows storable factory / GetId |
| 4 | Version | Windows storable envelope, normally zero |
| 8 | First bob ID | Windows drawing subtracts manager +04 |
| 12 | Number of 24-byte records | Windows manager +08; controls presence of memory objects |
| 16 | Packed stream byte count | Windows manager +0C; native save resizes to this size |
| 20 | Stored row descriptor count | Mac manager +14; matches Windows asset section size |
| 24 | Number of generated rows before table reuse | Mac AddBob counter +18; matched audit assets |
| 28 | Empty generated rows | Mac counter +1C; matched audit assets |
| 32 | Reused packed rows | Mac counter +20; zero in the installed corpus |

The last four fields were unresolved in the game C++ layout; their generator meaning is supported by Mac and asset comparisons, not a new game-side reconstruction claim. Empty managers have no memory objects. Preserve version, first ID, all counters, section versions and trailing bytes during serialization rather than replacing them with guessed zeroes.

## Memory objects

For nonempty managers, three objects follow: 12-byte envelope (`1001`, version, byte length) and the buffer. Their order is frame records, packed pixels, row descriptors.

Each frame is `uint type; int xOffset; int yOffset; uint width; uint height; uint firstRowIndex`. Offsets are signed placement offsets; firstRowIndex is an index, not a byte address. Type zero is an unallocated record. Do not guess another type as type 1.

A row descriptor is `indent << 22 | pixelOffset`. The upper ten bits are indentation and the lower 22 bits are a byte offset relative to the packed payload. **Only full FFFFFFFF denotes an empty row.** No subtract-12 or divide-by-four offset fallback is supported by the Windows code.

## Row streams

For types 1/2/4, a zero byte ends the row. The low seven bits of a nonzero control specify 1..127 pixels; bit 7 means skip without payload. Otherwise type 1 has one palette byte per pixel, type 2 has no payload, and type 4 has index/blend pairs. Index zero is legal in a literal; transparency belongs to the run structure. A type-4 literal blend byte zero is also distinct from a skipped pixel.

Type 3 uses `(count, timeValue)` pairs with counts 1..255 and one zero count terminator. Value 255 denotes skipped pixels. This type is represented in the installed corpus and its decode/repack matches byte exactly; do not apply the seven-bit control decoder to it.

The generated row starts at its first occupied pixel. Native packing omits trailing transparent runs, splits visible/skip runs at 127 (type 3 at 255), and terminates once. Fully empty rows allocate no pixel bytes. Mac memory optimization searches the existing pixel stream at all byte positions for the first complete encoded row match; frame line tables reuse the first matching contiguous descriptor sequence.

## Native references

- Windows `00410715` storable factory, `0041067E` envelope saving, `0043997D` Bob loading, `004399CF` Bob saving and `0042F91A` / `0042F95E` memory loading/saving.
- Windows `00439AB3` hit decoding and the rendering routines confirm the exact descriptor shift/mask, payload stride and FFFFFFFF check.
- Mac `CBobManager::AddBob`, `AddBob8BitDouble`, `l_Generate_PackLine_DoPack`, `l_Generate_PackLine_Double8BitBob`, `l_Generate_PackLine_TimeMaskBob`, `Generate_SetMemoryOptimizationFlag`, and `Storable_SaveData`.
- Local reference directory: `D:ProjekteC++culturesOpenVikings_ASM`.

The ordinary packing profile is empirically confirmed on the installed Windows assets. Optimized generation needs original optimized fixtures. Initial source bitmap cropping, palette quantization and anchor selection remain separate from packing already-cropped template frames.
