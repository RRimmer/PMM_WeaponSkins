"""Minimal binary KV3 (v4/v5, LZ4 or uncompressed) reader for Source 2 resources, enough to read vmdl_c DATA.
Port of the relevant parts of ValveResourceFormat's BinaryKV3.cs. Binary blobs are returned as None."""
import struct


def lz4_block(src):
    dst = bytearray()
    i, n = 0, len(src)
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
    out = {}
    for i in range(bcount):
        p = base + i * 12
        t = d[p:p + 4].decode('ascii', 'replace')
        off, ln = struct.unpack('<II', d[p + 4:p + 12])
        out.setdefault(t, (p + 4 + off, ln))
    return out


class Lane:
    def __init__(self, data, pos=0):
        self.d, self.p = data, pos

    def take(self, fmt):
        v = struct.unpack_from('<' + fmt, self.d, self.p)
        self.p += struct.calcsize('<' + fmt)
        return v[0] if len(v) == 1 else v


class Buf:
    pass


def _split(raw, off, c1, c2, c4, c8, v5):
    b = Buf()
    def al(o, a):
        return (o + a - 1) // a * a
    b.b1 = Lane(raw[off:off + c1]); off += c1
    if c2:
        off = al(off, 2)
    b.b2 = Lane(raw[off:off + c2 * 2]); off += c2 * 2
    if c4:
        off = al(off, 4)
    b.b4 = Lane(raw[off:off + c4 * 4]); off += c4 * 4
    if c8:
        off = al(off, 8)
    b.b8 = Lane(raw[off:off + c8 * 8]); off += c8 * 8
    return b, off


def parse(d, s):
    magic = struct.unpack_from('<I', d, s)[0]
    ver = magic & 0xff
    assert magic >> 8 == 0x4B5633 and ver in (4, 5), hex(magic)
    p = s + 4 + 16
    comp = struct.unpack_from('<I', d, p)[0]; p += 4
    p += 4  # dict id, frame size
    c1, c4, c8, ctypes = struct.unpack_from('<IIII', d, p); p += 16
    p += 4
    unc_total, comp_total, cblocks, blobs = struct.unpack_from('<IIII', d, p); p += 16
    c2, _bcs = struct.unpack_from('<II', d, p); p += 8
    if ver >= 5:
        unc1, comp1, unc2, comp2, c1b, c2b, c4b, c8b, _nodes, cobj, _arrs, _elems = struct.unpack_from('<12I', d, p)
        p += 48
    else:
        unc1, comp1 = unc_total, comp_total
    def get(n_comp):
        nonlocal p
        raw = d[p:p + n_comp]; p += n_comp
        return raw if comp == 0 else lz4_block(raw)
    if comp not in (0, 1):
        raise ValueError('zstd not supported')
    raw1 = get(comp1)
    b1, off = _split(raw1, 0, c1, c2, c4, c8, ver >= 5)
    nstr = b1.b4.take('i')
    # strings: v5 from bytes1 lane
    strings = []
    if ver >= 5:
        lane = b1.b1
    else:
        lane = Lane(raw1, off)
    for _ in range(nstr):
        e = lane.d.index(b'\0', lane.p)
        strings.append(lane.d[lane.p:e].decode('utf-8', 'replace'))
        lane.p = e + 1
    if ver >= 5:
        raw2 = get(comp2)
        objl = Lane(raw2[:cobj * 4])
        main, off2 = _split(raw2, cobj * 4, c1b, c2b, c4b, c8b, True)
        types = Lane(raw2[off2:off2 + ctypes])
        aux = b1
    else:
        main = b1
        types = Lane(raw1, off)
        objl = None
        aux = b1

    def rtype():
        t = types.take('B')
        if t & 0x80:
            types.take('B')
        if t & 0x40:
            types.take('B')
        return t & 0x3F

    def val(t, lane):
        if t == 1: return None
        if t == 13: return True
        if t == 14: return False
        if t in (15, 26): return 0
        if t in (16, 27): return 1
        if t == 17: return 0.0
        if t == 18: return 1.0
        if t == 2: return lane.b1.take('B') != 0
        if t == 22: return lane.b1.take('b')
        if t == 23: return lane.b1.take('B')
        if t == 20: return lane.b2.take('h')
        if t == 21: return lane.b2.take('H')
        if t == 11: return lane.b4.take('i')
        if t == 12: return lane.b4.take('I')
        if t == 19: return lane.b4.take('f')
        if t == 3: return lane.b8.take('q')
        if t == 4: return lane.b8.take('Q')
        if t == 5: return lane.b8.take('d')
        if t == 6:
            i = main.b4.take('i')
            return strings[i] if 0 <= i < len(strings) else ''
        if t == 7: return None
        if t == 8:
            n = main.b4.take('i')
            return [val(rtype(), main) for _ in range(n)]
        if t in (10, 24, 25):
            n = main.b4.take('i') if t == 10 else main.b1.take('B')
            st = rtype()
            el = aux if t == 25 else main
            return [val(st, el) for _ in range(n)]
        if t == 9:
            n = objl.take('i') if objl else main.b4.take('i')
            o = {}
            for _ in range(n):
                tt = rtype()
                name = strings[main.b4.take('i')]
                o[name] = val(tt, main)
            return o
        raise ValueError('kv3 type %d' % t)

    return val(rtype(), main)


def read_data(path):
    d = open(path, 'rb').read()
    s, ln = blocks(d)['DATA']
    return parse(d, s)
