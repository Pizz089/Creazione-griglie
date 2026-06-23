import re, shutil

SRC = r"\\10.11.1.1\c$\Program Files (x86)\Steltronic\Vision\MediaNova\MeshBase\Styles\style#1\p10"
DST = r"\\10.11.1.1\c$\Program Files (x86)\Steltronic\Vision\MediaNova\MeshBase\Styles\style#15\p10"

def match_brace(s, o):
    d = 0
    for i in range(o, len(s)):
        if s[i] == '{': d += 1
        elif s[i] == '}':
            d -= 1
            if d == 0: return i
    return -1

MAGENTA = """Material {
 1.000000;0.000000;1.000000;1.000000;;
0.000000;
 0.000000;0.000000;0.000000;;
 1.000000;0.000000;1.000000;;
 }"""

def build_outline(block, scale=1.2, zoff=0.3):
    m = re.match(r"Mesh\s+(\w+)\s*\{", block)
    name = m.group(1)
    cm = re.compile(r"\s*(\d+)\s*;").match(block, m.end())
    nv = int(cm.group(1))
    vp = re.compile(r"\s*(-?\d+\.\d+);(-?\d+\.\d+);(-?\d+\.\d+);[,;]")
    p = cm.end(); verts = []; vstart = None; vend = None
    for _ in range(nv):
        vm = vp.match(block, p)
        if vstart is None: vstart = vm.start()
        verts.append([float(vm.group(1)), float(vm.group(2)), float(vm.group(3))])
        vend = p = vm.end()
    cx = (min(v[0] for v in verts) + max(v[0] for v in verts)) / 2
    cy = (min(v[1] for v in verts) + max(v[1] for v in verts)) / 2
    lines = []
    for i, (x, y, z) in enumerate(verts):
        nx = cx + (x - cx) * scale
        ny = cy + (y - cy) * scale
        nz = z + zoff
        lines.append(f"{nx:.6f};{ny:.6f};{nz:.6f};" + ("," if i < nv - 1 else ";"))
    nb = block[:vstart] + "\n" + "\n".join(lines) + "\n" + block[vend:]
    nb = re.sub(r"\bMaterial\s*\{[^}]*\}", MAGENTA, nb)
    nb = re.sub(r"Mesh\s+" + re.escape(name) + r"\s*\{", f"Mesh {name}_OUT {{", nb, count=1)
    return nb

def is_template(s, idx):
    j = idx - 1
    while j > 0 and s[j] in " \t\r\n": j -= 1
    return j >= 7 and s[j-7:j+1] == "template"

total = 0
for n in range(1, 10):
    fn = f"t_{n}.x"
    txt = open(f"{SRC}\\{fn}", encoding="latin-1").read()
    inserts = []
    for m in re.finditer(r"\bMesh\s+\w+\s*\{", txt):
        if is_template(txt, m.start()): continue
        o = txt.index('{', m.start()); e = match_brace(txt, o)
        inserts.append((e + 1, "\n " + build_outline(txt[m.start():e+1])))
    for pos, t in sorted(inserts, key=lambda x: -x[0]):
        txt = txt[:pos] + t + txt[pos:]
    bal = txt.count('{') == txt.count('}')
    open(f"{DST}\\{fn}", "w", encoding="latin-1").write(txt)
    total += len(inserts)
    print(f"{fn}: copie bordo={len(inserts)}, graffe {'OK' if bal else 'SBILANCIATE'}, _OUT={txt.count('_OUT')}")

# ripristino player.X originale (rimuovo il test rosso precedente)
shutil.copy(f"{SRC}\\player.X", f"{DST}\\player.X")
print(f"\nplayer.X ripristinato all'originale.")
print(f"Totale copie bordo inserite: {total}")
