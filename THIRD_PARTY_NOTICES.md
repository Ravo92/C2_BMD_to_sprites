# Third-party image libraries

The converter uses these pinned NuGet packages for PNG decoding and encoding:

| Package | Version | Upstream license |
| --- | --- | --- |
| [StbImageSharp](https://github.com/StbSharp/StbImageSharp) | 2.30.16 | Unlicense OR MIT, as declared in the package metadata |
| [StbImageWriteSharp](https://github.com/StbSharp/StbImageWriteSharp) | 1.16.7 | Public domain, as declared by the upstream README |

Both are C# ports of Sean Barrett and contributors' [stb libraries](https://github.com/nothings/stb). They require no native runtime binaries or license activation. Their original project notices and licensing terms remain applicable.

`RgbaImage` isolates the converter's straight RGBA pixel buffers from these libraries. BMD packing and palette selection do not depend on the PNG library. PNG compression may produce different file bytes than earlier exports; decoded pixels and the reconstructed BMD retain the same values.

SixLabors.ImageSharp is no longer a direct or transitive dependency of this project. No Six Labors license key or license file is needed to restore, build or run the converter.
