# BMD packing audit - 2026-10-06

## Result

All **206 installed BMDs** under `E:SteamsteamappscommonCultures 8th Wonderdata` were independently decoded and regenerated using the nonoptimized packing profile and recovered AddBob order. **206 / 206 are byte-identical**, including frame records, packed pixels, row descriptors, header counters and SHA-256. Total input size is **133,294,533 bytes**; **267,386 frame records**, nine empty managers. The corpus includes types 0, 1, 2, 3 and 4. It includes both the installed GUI window file and the user's `_original` copy, which have the same hash. This count measures files, not unique content or game-function completion.

[Per-file audit CSV](BMD_CORPUS_AUDIT.csv) records relative paths, lengths, frame counts, both hashes and table equality. Game resources are not copied into this repository.

## What changed the first result

The first ID-order GUI repack had identical decoded pixels/anchors and equal length (937,075 bytes) but differed at 2,316 byte positions, beginning at offset 164. Its stored row bases show Sprite 4 was added after Sprite 12. Recreating the stored creation order removes those differences completely. The GUI window SHA-256 is `1DEB6437EB2FB2806CD5E43EF9508D65A06E187F507229368335C7BB8C579631`.

Both actual PNG round trips of this 193-frame GUI resource are byte-identical: colored previews with unchanged-pixel template hints, and raw index/second-channel/occupancy PNGs. These are distinct from the binary reader/writer rewrite command. Preview hints are necessary because RGB alone loses duplicate palette indices and literal alpha-zero occupancy.

## Focused verification

The C# converter was compiled with the locally installed .NET 10.0.401 SDK, existing restored ImageSharp 3.1.12 dependency and a `ConverterAudit` configuration: zero errors, zero warnings. No native OpenVikings Debug/Release build and no test project was added.

Temporary conversion fixtures checked 127-pixel literal/skip boundaries, empty rows, index-zero / alpha-zero type-4 literals, and 255-length time-mask runs. A raw PNG round trip retained the zero-index/zero-alpha literal byte exactly. Truncation, unknown type, oversized run, missing terminator and invalid offset returned explicit CLI errors. A repeated-row/two-frame mask fixture exercised both optimization reuse stages and preserved decoded pixels; this only validates that candidate implementation, not byte identity with original optimized art assets.

The installed corpus has **zero headers with a nonzero reused-row counter**; all 206 match without optimization. Do not generalize this result to optimized assets without input evidence.

## Remaining scope

The algorithm now reproduces the stored Windows corpus from decoded values plus original metadata and creation order. It does not reconstruct the original uncropped artwork, choose palettes/anchors for arbitrary PNGs, prove the art tool's quantizer, or recover lost creation history from optimized row reuse. The current PNG importer deliberately requires a template and the same frame dimensions. The inherited GUI palette override table is a separate policy, not stored in BMD files.

Next: explicit editable frame manifest / new-frame anchor and crop handling; original optimized fixtures if available. Keep shader previews separate from exact raw index data and retain per-file byte comparisons when extending the generator.

## Separate game continuation

The C++ game checkpoint remains `5cc139f` with the partial human idle lifecycle in `5e2c4db`. Continue it in the new chat from `D:ProjekteC++culturesOpenVikings_cppdocsNEXT_CHAT_HANDOFF.md`: task-24 goodie/talk/sleep helpers, atomic events/conditions, then walking/path-step callbacks. Converter work does not increase the game's address-coverage numerator.
