import struct, sys, os, re

def read_cstr(f):
    b = bytearray()
    while True:
        c = f.read(1)
        if not c or c == b'\0':
            return b.decode('utf-8', 'replace')
        b += c

def index(dirpath):
    out = {}
    with open(dirpath, 'rb') as f:
        sig, ver, tree = struct.unpack('<III', f.read(12))
        assert sig == 0x55aa1234, hex(sig)
        if ver == 2:
            f.read(16)
        hdr = f.tell()
        while True:
            ext = read_cstr(f)
            if not ext:
                break
            while True:
                path = read_cstr(f)
                if not path:
                    break
                while True:
                    name = read_cstr(f)
                    if not name:
                        break
                    crc, pre, arc, off, ln, term = struct.unpack('<IHHIIH', f.read(18))
                    predata = f.read(pre)
                    full = (path.strip() + '/' if path.strip() not in ('', ' ') else '') + name + '.' + ext
                    out[full] = (arc, off, ln, predata)
        data_start = hdr + tree
    return out, data_start

def extract(dirpath, pattern, outdir):
    idx, data_start = index(dirpath)
    base = dirpath[:-len('_dir.vpk')]
    rx = re.compile(pattern)
    n = 0
    for name, (arc, off, ln, pre) in sorted(idx.items()):
        if not rx.search(name):
            continue
        if arc == 0x7fff:
            src = dirpath; off += data_start
        else:
            src = '%s_%03d.vpk' % (base, arc)
        with open(src, 'rb') as f:
            f.seek(off)
            data = pre + f.read(ln)
        dst = os.path.join(outdir, name.replace('/', '__'))
        with open(dst, 'wb') as o:
            o.write(data)
        n += 1
        print(name, len(data))
    print('extracted', n)

if __name__ == '__main__':
    if sys.argv[1] == 'list':
        idx, _ = index(sys.argv[2])
        rx = re.compile(sys.argv[3])
        for k in sorted(idx):
            if rx.search(k):
                print(k, idx[k][2] + len(idx[k][3]))
    else:
        os.makedirs(sys.argv[4], exist_ok=True)
        extract(sys.argv[2], sys.argv[3], sys.argv[4])
