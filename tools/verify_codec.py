"""Independent BMD/PNG fixtures and CLI regression checks; Python standard library only.

Build the C# project first, then pass its DLL. Optional --corpus probes installed
BMDs read-only. No game files are copied into the repository.
"""
import argparse
import json
from pathlib import Path
import struct
import subprocess
import tempfile
import zlib


def chunk(kind, data):
    return struct.pack('>I', len(data)) + kind + data + struct.pack('>I', zlib.crc32(kind + data))


def png_data(path, width, height, rows, *, depth=8, color=6, interlace=0, extra=()):
    path.write_bytes(b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', width, height, depth, color, 0, 0, interlace)) + b''.join(chunk(kind, data) for kind, data in extra) + chunk(b'IDAT', zlib.compress(rows)) + chunk(b'IEND', b''))


def png(path, width, height, pixels):
    rows = b''.join(b'\0' + bytes(sum(pixels[y * width:(y + 1) * width], ())) for y in range(height))
    png_data(path, width, height, rows)


def bmd(path, frames, pixels, rows, first_id=0):
    header = struct.pack('<9I', 1012, 7, first_id, len(frames), len(pixels), len(rows), len(rows), rows.count(0xffffffff), 0)
    if not frames:
        path.write_bytes(header)
        return
    blocks = [b''.join(struct.pack('<IiiIII', *f) for f in frames), bytes(pixels), struct.pack(f'<{len(rows)}I', *rows)]
    path.write_bytes(header + b''.join(struct.pack('<III', 1001, 11 + i, len(block)) + block for i, block in enumerate(blocks)) + b'tail')


def unpack(path):
    """Independent literal/skip decoder, returning metadata and occupied values."""
    data = path.read_bytes()
    header = struct.unpack_from('<9I', data)
    blocks, pos = [], 36
    if not header[3]:
        return header, [], [], [], []
    for _ in range(3):
        kind, version, size = struct.unpack_from('<III', data, pos)
        assert kind == 1001
        blocks.append(data[pos + 12:pos + 12 + size])
        pos += 12 + size
    frames = list(struct.iter_unpack('<IiiIII', blocks[0]))
    rows = list(struct.unpack(f'<{len(blocks[2]) // 4}I', blocks[2]))
    images = []
    for kind, _, _, width, height, base in frames:
        if not kind:
            images.append([])
            continue
        image = [None] * (width * height)
        for y in range(height):
            descriptor = rows[base + y]
            if descriptor == 0xffffffff:
                continue
            x, cursor = descriptor >> 22, descriptor & 0x3fffff
            while blocks[1][cursor]:
                control = blocks[1][cursor]
                cursor += 1
                count = control if kind == 3 else control & 127
                assert count and x + count <= width
                if kind != 3 and control & 128:
                    x += count
                    continue
                for k in range(count):
                    if kind == 2:
                        value = 255
                    elif kind == 3:
                        value = blocks[1][cursor]
                    elif kind == 4:
                        value = struct.unpack_from('<H', blocks[1], cursor + k * 2)[0]
                    else:
                        value = blocks[1][cursor + k]
                    image[y * width + x] = None if kind == 3 and value == 255 else value
                    x += 1
                cursor += {1: count, 2: 0, 3: 1, 4: count * 2}[kind]
        images.append(image)
    return header, frames, images, rows, blocks[1]


def verify(dll, root):
    checks = []

    def cli(*args, code=0, cwd=None, apphost=False):
        executable = [str(dll.with_suffix('.exe'))] if apphost else ['dotnet', str(dll)]
        result = subprocess.run([*executable, *map(str, args)], cwd=cwd, capture_output=True, text=True, encoding='utf-8', errors='replace')
        assert result.returncode == code, (args, result.returncode, result.stdout, result.stderr)
        return result.stdout

    def check(name):
        checks.append(name)
        print('PASS', name, flush=True)

    # Independently packed run boundaries, all types, non-ID generation order,
    # empty/zero-sized/unallocated records, signed offsets and versioned envelopes.
    payload, rows, frames = bytearray(), [], [None] * 7
    for index, kind, width, streams in [
        (3, 4, 260, [b'\x7f' + b'\0\0' * 127 + b'\xff\x81\x02\x07\x80\0\0\0', None]),
        (1, 1, 260, [b'\x7f' + bytes(range(127)) + b'\xff\x81\x02\0\xff\0', None]),
        (2, 2, 260, [b'\x7f\xff\x81\x02\0', None]),
        (4, 3, 520, [b'\xff\x09\xff\xff\x0a\x0b\0', None]),
    ]:
        base = len(rows)
        for stream in streams:
            if stream is None:
                rows.append(0xffffffff)
            else:
                rows.append(len(payload))
                payload.extend(stream)
        frames[index] = (kind, -17, 21, width, len(streams), base)
    frames[0] = (0, -3, 5, 0xffffffff, 0xffffffff, 0xffffffff)
    frames[5] = (1, -1, -2, 0, 2, len(rows))
    rows.extend([0xffffffff] * 2)
    frames[6] = (2, 0, 0, 1, 0, len(rows))
    source = root / 'fixture.bmd'
    bmd(source, frames, payload, rows, first_id=0x80000000)
    expected = unpack(source)
    assert json.loads(cli('probe', source))['ByteIdentical']
    cli('decode', source, root / 'raw')
    manifest = root / 'raw/fixture.frames.json'
    for command, args in [('encode', [source, root / 'raw']), ('build', [manifest])]:
        output = root / f'{command}.bmd'
        cli(command, *args, output)
        assert source.read_bytes() == output.read_bytes()
    check('all bob types, 127/255 boundaries, empty records, metadata and raw manifest/template roundtrips')
    optimized = root / 'optimized.bmd'
    cli('repack', source, optimized, '--optimize')
    assert unpack(optimized)[2] == expected[2]
    check('optimized streams independently decode to identical occupied values')
    repeated = root / 'repeated.bmd'
    bmd(repeated, [(2, 0, 0, 3, 2, 0), (2, 0, 0, 3, 2, 2)], [3, 0] * 4, [0, 2, 4, 6])
    cli('repack', repeated, root / 'reused.bmd', '--optimize')
    reused = unpack(root / 'reused.bmd')
    assert reused[2] == unpack(repeated)[2] and reused[0][8] == 3
    assert reused[1][0][5] == reused[1][1][5] == 0 and len(reused[4]) == 2
    check('both packed-row reuse and contiguous frame-row-table reuse')

    empty = root / 'empty.bmd'
    bmd(empty, [], [], [])
    cli('decode', empty, root / 'empty')
    cli('build', root / 'empty/empty.frames.json', root / 'empty-built.bmd')
    assert empty.read_bytes() == (root / 'empty-built.bmd').read_bytes()
    check('empty managers do not acquire CMemory objects')

    document = json.loads(manifest.read_text())
    document['Frames'][1]['XOffset'] = -125
    manifest.write_text(json.dumps(document))
    cli('build', manifest, root / 'moved.bmd')
    assert unpack(root / 'moved.bmd')[1][1][1] == -125
    check('manifest anchor edits and frame IDs above Int32.MaxValue')

    # Palette entries 3 and 7 deliberately share RGB. Changing only alpha must
    # preserve index 7; index-zero/alpha-zero occupied pixels must survive too.
    pcx = bytearray(128)
    pcx[0], pcx[3], pcx[65] = 10, 8, 1
    colors = bytearray(768)
    colors[3 * 3:3 * 3 + 3] = colors[7 * 3:7 * 3 + 3] = bytes([40, 50, 60])
    palette = root / 'palette.pcx'
    palette.write_bytes(pcx + b'\x0c' + colors)
    alpha = root / 'alpha.bmd'
    bmd(alpha, [(4, -4, 8, 3, 1, 0)], [3, 7, 200, 0, 0, 0, 255, 0], [0], first_id=10)
    cli('decode', alpha, root / 'color', palette)
    cli('encode', alpha, root / 'color', root / 'alpha-same.bmd', palette)
    assert alpha.read_bytes() == (root / 'alpha-same.bmd').read_bytes()
    cli('build', root / 'color/alpha.frames.json', root / 'alpha-raw.bmd')
    cli('build', root / 'color/alpha.frames.json', root / 'alpha-preview.bmd', '--preview')
    assert alpha.read_bytes() == (root / 'alpha-raw.bmd').read_bytes() == (root / 'alpha-preview.bmd').read_bytes()
    png(root / 'color/alpha_000.png', 3, 1, [(40, 50, 60, 100), (0, 0, 0, 0), (0, 0, 0, 255)])
    cli('encode', alpha, root / 'color', root / 'alpha-edit.bmd', palette)
    assert unpack(root / 'alpha-edit.bmd')[2][0] == [7 | 100 << 8, 0, 255 << 8]
    hidden = root / 'hidden-preview.bmd'
    bmd(hidden, [(4, 0, 0, 1, 1, 0)], [1, 7, 0, 0], [0])
    cli('decode', hidden, root / 'hidden-preview', palette)
    cli('encode', hidden, root / 'hidden-preview', root / 'hidden-preview-built.bmd', palette)
    assert hidden.read_bytes() == (root / 'hidden-preview-built.bmd').read_bytes()
    check('single PCX, preview manifest import, duplicate indices and occupied alpha-zero pixels')

    dropped = root / 'dropped asset.BMD'
    dropped.write_bytes(alpha.read_bytes())
    working = root / 'working directory'
    working.mkdir()
    cli(dropped, cwd=working, apphost=True)
    first_export = working / 'dropped asset_sprites'
    assert (first_export / 'dropped asset_000.indices.png').exists()
    assert not (root / 'dropped asset_sprites').exists()
    cli('build', first_export / 'dropped asset.frames.json', root / 'dropped-rebuilt.bmd')
    assert dropped.read_bytes() == (root / 'dropped-rebuilt.bmd').read_bytes()
    (first_export / 'keep.txt').write_text('preserved')
    # Also skip a colliding ordinary file, not only existing directories.
    (working / 'dropped asset_sprites_2').write_text('preserved file')
    dropped.with_suffix('.pcx').write_bytes(palette.read_bytes())
    cli(dropped, cwd=working, apphost=True)
    assert (working / 'dropped asset_sprites_3/dropped asset_000.png').exists()
    assert (first_export / 'keep.txt').read_text() == 'preserved'
    assert (working / 'dropped asset_sprites_2').read_text() == 'preserved file'
    dropped.with_suffix('.palettes.json').write_text(json.dumps({'Default': 'palette.pcx'}))
    # A valid JSON map takes precedence over a broken same-name PCX.
    dropped.with_suffix('.pcx').write_bytes(b'not a PCX')
    cli(dropped, cwd=working, apphost=True)
    assert (working / 'dropped asset_sprites_4/dropped asset_000.png').exists()
    check('EXE drag-and-drop arguments, working directory, spaces, folder collisions and palette sidecars')
    gui = root / 'gui'
    gui.mkdir()
    (gui / 'frame.pcx').write_bytes(palette.read_bytes())
    cli('decode', alpha, root / 'gui-export', gui, 'ls_gui_window.bmd')
    cli('encode', alpha, root / 'gui-export', root / 'gui.bmd', gui, 'ls_gui_window.bmd')
    assert alpha.read_bytes() == (root / 'gui.bmd').read_bytes()
    check('legacy GUI rules load only palettes required by present frames')

    mapping = root / 'palettes.json'
    mapping.write_text(json.dumps({'Ranges': [{'FirstBobId': 10, 'LastBobId': 10, 'Palette': 'palette.pcx'}]}))
    cli('decode', alpha, root / 'mapped', mapping)
    cli('build', root / 'mapped/alpha.frames.json', root / 'mapped.bmd', '--preview')
    assert alpha.read_bytes() == (root / 'mapped.bmd').read_bytes()
    mapping.write_text(json.dumps({'Ranges': [{'FirstBobId': 11, 'LastBobId': 12, 'Palette': 'palette.pcx'}]}))
    cli('decode', alpha, root / 'missing-palette', mapping, code=1)
    assert not (root / 'missing-palette').exists()
    mapping.write_text(json.dumps({'Ranges': [{'FirstBobId': 10, 'LastBobId': 12, 'Palette': 'palette.pcx'}, {'FirstBobId': 12, 'LastBobId': 13, 'Palette': 'palette.pcx'}]}))
    cli('decode', alpha, root / 'overlap', mapping, code=1)
    cli('decode', alpha, root / 'wrong-gui', root, code=1)
    assert not (root / 'wrong-gui').exists()
    check('relative JSON palettes and explicit errors for uncovered/overlapping/unknown GUI assignments')

    # A handcrafted manifest produces a new manager without a BMD template.
    png(root / 'new.indices.png', 2, 1, [(0, 45, 0, 255), (8, 0, 0, 255)])
    fresh = root / 'new.frames.json'
    fresh.write_text(json.dumps({'FormatVersion': 1, 'LogicalName': 'new.bmd', 'FirstBobId': 42, 'Frames': [{'Type': 4, 'XOffset': -5, 'YOffset': 8, 'Width': 2, 'Height': 1, 'RawImage': 'new.indices.png'}]}))
    cli('build', fresh, root / 'new.bmd')
    assert unpack(root / 'new.bmd')[0][2] == 42
    assert unpack(root / 'new.bmd')[2][0] == [45 << 8, 8]
    # Test editor-compatible indexed/tRNS, 16-bit, Sub-filtered and Adam7 PNGs.
    compatible = root / 'compatible.indices.png'
    compatible_manifest = root / 'compatible.frames.json'
    compatible_manifest.write_text(json.dumps({'FormatVersion': 1, 'LogicalName': 'compatible.bmd', 'Frames': [{'Type': 4, 'Width': 2, 'Height': 1, 'RawImage': compatible.name}]}))
    samples = [
        ('indexed-trns', b'\0\x40', {'depth': 1, 'color': 3, 'extra': [(b'PLTE', bytes([7, 0, 0, 9, 21, 0])), (b'tRNS', bytes([255, 255]))]}, [7, 9 | 21 << 8]),
        ('rgba16', b'\0' + struct.pack('>8H', 7 * 257, 0, 0, 65535, 9 * 257, 21 * 257, 0, 65535), {'depth': 16}, [7, 9 | 21 << 8]),
        ('sub-filter', bytes([1, 7, 0, 0, 255, 2, 21, 0, 0]), {}, [7, 9 | 21 << 8]),
        # Two pixels occupy Adam7 passes 1 and 6; each pass has its own filter byte.
        ('adam7', bytes([0, 7, 0, 0, 255, 0, 9, 21, 0, 255]), {'interlace': 1}, [7, 9 | 21 << 8]),
    ]
    for name, scanlines, options, expected_pixels in samples:
        png_data(compatible, 2, 1, scanlines, **options)
        compatible_output = root / f'{name}.bmd'
        cli('build', compatible_manifest, compatible_output)
        assert unpack(compatible_output)[2][0] == expected_pixels, name
    png_data(compatible, 2, 1, b'\0\x40', depth=1, color=3, extra=[(b'PLTE', bytes([7, 0, 0, 9, 21, 0])), (b'tRNS', bytes([0, 255]))])
    cli('build', compatible_manifest, root / 'hidden-rgb.bmd')
    assert unpack(root / 'hidden-rgb.bmd')[2][0] == [None, 9 | 21 << 8]
    # The original palette index 9 still comes from PNG RGB, not its PLTE slot 1.
    png_data(compatible, 2, 1, bytes([0, 7, 0, 0, 255, 9, 21, 0, 255]), extra=[(b'acTL', struct.pack('>II', 1, 0))])
    cli('build', compatible_manifest, root / 'animated.bmd', code=1)
    # A later IHDR must not replace dimensions checked before image allocation.
    png_data(compatible, 2, 1, bytes([0, 7, 0, 0, 255, 9, 21, 0, 255]), extra=[(b'IHDR', struct.pack('>IIBBBBB', 2, 1, 8, 6, 0, 0, 0))])
    cli('build', compatible_manifest, root / 'duplicate-header.bmd', code=1)
    check('indexed/tRNS, 16-bit, filtered and Adam7 PNGs; APNG/duplicate-header rejection')
    cli('decode', root / 'new.bmd', root / 'new-export')
    (root / 'new-export/new_000.indices.png').write_bytes(b'existing sentinel')
    (root / 'new-export/new.frames.json').unlink()
    cli('decode', root / 'new.bmd', root / 'new-export', code=1)
    assert (root / 'new-export/new_000.indices.png').read_bytes() == b'existing sentinel'
    cli('build', fresh, root / 'new.bmd', code=1)
    cli('build', fresh, fresh, '--force', code=1)
    check('new manager creation and collision/input protection')

    png(root / 'new.indices.png', 2, 1, [(0, 45, 0, 128), (8, 0, 0, 255)])
    cli('build', fresh, root / 'bad-occupancy.bmd', code=1)
    png(root / 'new.indices.png', 1, 1, [(0, 0, 0, 255)])
    cli('build', fresh, root / 'bad-size.bmd', code=1)
    (root / 'new.indices.png').write_bytes(b'GIF89a' + bytes(30))
    cli('build', fresh, root / 'not-png.bmd', code=1)
    png(root / 'color/alpha_000.png', 3, 1, [(1, 2, 3, 255), (0, 0, 0, 0), (0, 0, 0, 255)])
    cli('encode', alpha, root / 'color', root / 'off-palette.bmd', palette, code=1)
    check('invalid occupancy, image dimensions, non-PNG content and off-palette colors')

    # Damaged later frames must be rejected even when an earlier frame already
    # differs, and rewrite must validate streams before copying them.
    valid = root / 'valid.bmd'
    bmd(valid, [(1, 0, 0, 1, 1, 0), (1, 0, 0, 1, 1, 1)], [1, 3, 0, 1, 4, 0], [0, 3])
    fewer = root / 'fewer.bmd'
    bmd(fewer, [(1, 0, 0, 1, 1, 0)], [1, 3, 0], [0])
    assert json.loads(cli('compare', valid, fewer, code=2))['FirstDifferentFrame'] == 1
    bmd(fewer, [(1, 5, 0, 1, 1, 0)], [1, 3, 0], [0])
    assert json.loads(cli('compare', valid, fewer, code=2))['FirstDifferentFrame'] == 0
    for name, frame_type, width, stream, descriptor in [
        ('unknown-type', 5, 1, [0], 0), ('oversized-run', 1, 1, [2, 1, 2, 0], 0),
        ('zero-run', 1, 1, [128, 0], 0), ('missing-terminator', 1, 1, [1, 7], 0),
        ('truncated-literal', 4, 1, [1, 7], 0), ('bad-offset', 1, 1, [0], 20),
        ('bad-indent', 1, 1, [1, 7, 0], 1 << 22), ('huge-frame', 1, 8193, [0], 0),
    ]:
        malformed = root / f'{name}.bmd'
        # First frame differs from valid.bmd; second carries the corruption.
        bmd(malformed, [(1, 5, 0, 1, 0, 0), (frame_type, 0, 0, width, 1, 0)], stream, [descriptor])
        cli('compare', valid, malformed, code=1)
        cli('rewrite', malformed, root / f'{name}-output.bmd', code=1)
        assert not (root / f'{name}-output.bmd').exists()
    cli('--force', code=1)
    fresh.write_text('{broken')
    before = (root / 'new.bmd').read_bytes()
    cli('build', fresh, root / 'new.bmd', '--force', code=1)
    assert (root / 'new.bmd').read_bytes() == before
    check('malformed streams/JSON/options are rejected before outputs change')
    return cli, checks


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('dll', type=Path)
    parser.add_argument('--corpus', type=Path)
    parser.add_argument('--gui-bmd', type=Path)
    parser.add_argument('--gui-palettes', type=Path)
    args = parser.parse_args()
    dll = args.dll.resolve()
    scratch = Path(__file__).resolve().parents[1] / 'obj'
    scratch.mkdir(exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='codec-audit-', dir=scratch) as directory:
        cli, checks = verify(dll, Path(directory))
        if args.gui_bmd or args.gui_palettes:
            assert args.gui_bmd and args.gui_palettes, 'Pass both --gui-bmd and --gui-palettes'
            gui_dir = Path(directory) / 'original-gui'
            cli('decode', args.gui_bmd, gui_dir, args.gui_palettes, 'ls_gui_window.bmd')
            manifest = gui_dir / 'ls_gui_window.frames.json'
            for label, arguments in [
                ('raw', ['build', manifest]), ('preview', ['build', manifest]),
                ('template', ['encode', args.gui_bmd, gui_dir]),
            ]:
                output = Path(directory) / f'original-gui-{label}.bmd'
                options = ['--preview'] if label == 'preview' else [str(args.gui_palettes), 'ls_gui_window.bmd'] if label == 'template' else []
                cli(*arguments, output, *options)
                assert output.read_bytes() == args.gui_bmd.read_bytes(), label
            checks.append('installed GUI raw/preview manifest and template PNG roundtrips')
            print('PASS', checks[-1], flush=True)
        total = size = frames = 0
        if args.corpus:
            for path in sorted(args.corpus.rglob('*')):
                if path.suffix.lower() != '.bmd' or not path.is_file():
                    continue
                report = json.loads(cli('probe', path))
                assert report['ByteIdentical'], str(path)
                total += 1
                size += path.stat().st_size
                frames += report['Original']['Header']['FrameCount']
                if total % 25 == 0:
                    print(f'PASS corpus {total} files', flush=True)
        print(json.dumps({'RegressionGroups': len(checks), 'CorpusFiles': total, 'CorpusBytes': size, 'CorpusFrameRecords': frames}, indent=2))


if __name__ == '__main__':
    main()
