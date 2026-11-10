"""Extract m_keyValueText (StickerMarkup / KeychainMarkup) from CS2 vmdl_c files.
usage: python3 vmdl_markup.py <dir with vmdl_c> <out.json>"""
import struct, sys, os, json, re


def lz4_block(src, out_size):
    dst = bytearray()
    i = 0
    n = len(src)
    while i < n:
        token = src[i]; i += 1
        lit = token >> 4
        if lit == 15:
            while True:
                b = src[i]; i += 1
                lit += b
                if b != 255:
                    break
        dst += src[i:i + lit]; i += lit
        if i >= n:
            break
        off = src[i] | (src[i + 1] << 8); i += 2
        ml = token & 15
        if ml == 15:
            while True:
                b = src[i]; i += 1
                ml += b
                if b != 255:
                    break
        ml += 4
        start = len(dst) - off
        if off >= ml:
            dst += dst[start:start + ml]
        else:
            for k in range(ml):
                dst.append(dst[start + k])
    return bytes(dst)


def blocks(d):
    size, hv, ver, boff, bcount = struct.unpack('<IHHII', d[:16])
    base = 8 + boff
    for i in range(bcount):
        p = base + i * 12
        t = d[p:p + 4]
        off, ln = struct.unpack('<II', d[p + 4:p + 12])
        yield t, p + 4 + off, ln


def kv3_buffer1(d, s):
    magic = struct.unpack('<I', d[s:s + 4])[0]
    ver = magic & 0xff
    assert magic >> 8 == 0x4B5633, hex(magic)
    p = s + 4 + 16
    comp = struct.unpack('<I', d[p:p + 4])[0]; p += 4
    dict_id, frame = struct.unpack('<HH', d[p:p + 4]); p += 4
    cb1, cb4, cb8, ctypes = struct.unpack('<IIII', d[p:p + 16]); p += 16
    p += 4  # objects, arrays
    unc_total, comp_total, cblocks, blobs = struct.unpack('<IIII', d[p:p + 16]); p += 16
    if ver >= 4:
        p += 8
    if ver >= 5:
        unc1, comp1 = struct.unpack('<II', d[p:p + 8])
        p += 48
    else:
        unc1, comp1 = unc_total, comp_total
    raw = d[p:p + comp1]
    if comp == 0:
        return raw
    if comp == 1:
        return lz4_block(raw, unc1)
    raise ValueError('zstd not supported')


def markup_text(path):
    d = open(path, 'rb').read()
    for t, s, ln in blocks(d):
        if t != b'DATA':
            continue
        buf = kv3_buffer1(d, s)
        for chunk in buf.split(b'\0'):
            if b'StickerMarkup' in chunk or b'KeychainMarkup' in chunk:
                return chunk.decode('utf-8', 'replace')
    return None


if __name__ == '__main__':
    src, out = sys.argv[1], sys.argv[2]
    res = {}
    for f in sorted(os.listdir(src)):
        if not f.endswith('.vmdl_c') or f.endswith('_mag.vmdl_c'):
            continue
        try:
            txt = markup_text(os.path.join(src, f))
        except Exception as e:
            txt = 'ERROR ' + repr(e)
        key = re.sub(r'^.*__', '', f)[:-7]
        res[key] = txt
        print(key, 'none' if txt is None else len(txt))
    json.dump(res, open(out, 'w'), indent=1)
