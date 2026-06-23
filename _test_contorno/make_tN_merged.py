import re

SRC = r"\\10.11.1.1\c$\Program Files (x86)\Steltronic\Vision\MediaNova\MeshBase\Styles\style#1\p10"
DST = r"\\10.11.1.1\c$\Program Files (x86)\Steltronic\Vision\MediaNova\MeshBase\Styles\style#15\p10"

NUMf = r"-?\d+(?:\.\d+)?"
VP = re.compile(rf"\s*({NUMf});({NUMf});({NUMf});\s*[,;]")
IP = re.compile(r"\s*(\d+);([-\d,]+);\s*[,;]")
INT = re.compile(r"\s*(\d+)\s*;")

OUT_MAT = """Material {
 1.000000;0.000000;1.000000;1.000000;;
0.000000;
 0.000000;0.000000;0.000000;;
 1.000000;0.000000;1.000000;;
 }"""

def match_brace(s, o):
    d = 0
    for i in range(o, len(s)):
        if s[i] == '{': d += 1
        elif s[i] == '}':
            d -= 1
            if d == 0: return i
    return -1

def parse_mesh(block):
    m = re.match(r"Mesh\s+(\w+)\s*\{", block); name = m.group(1); pos = m.end()
    mo = INT.match(block, pos); nv = int(mo.group(1)); pos = mo.end()
    verts = []
    for _ in range(nv):
        vm = VP.match(block, pos); verts.append([vm.group(1), vm.group(2), vm.group(3)]); pos = vm.end()
    mo = INT.match(block, pos); nf = int(mo.group(1)); pos = mo.end()
    faces = []
    for _ in range(nf):
        fm = IP.match(block, pos); faces.append([int(x) for x in fm.group(2).split(',')]); pos = fm.end()
    mml_o = block.index("{", block.index("MeshMaterialList", pos)); mml_e = match_brace(block, mml_o)
    orig_mat = re.search(r"\bMaterial\s*\{[^}]*\}", block[mml_o:mml_e]).group(0)
    mn_o = block.index("{", block.index("MeshNormals", mml_e)); mn_e = match_brace(block, mn_o)
    mn = block[mn_o+1:mn_e]; pos = 0
    mo = INT.match(mn, pos); nn = int(mo.group(1)); pos = mo.end()
    normals = []
    for _ in range(nn):
        vm = VP.match(mn, pos); normals.append([vm.group(1), vm.group(2), vm.group(3)]); pos = vm.end()
    mo = INT.match(mn, pos); nfn = int(mo.group(1)); pos = mo.end()
    fnorm = []
    for _ in range(nfn):
        fm = IP.match(mn, pos); fnorm.append([int(x) for x in fm.group(2).split(',')]); pos = fm.end()
    return name, verts, faces, orig_mat, normals, fnorm

def vlist(vs):
    return "\n".join(f"{x};{y};{z};" + ("," if i < len(vs)-1 else ";") for i, (x, y, z) in enumerate(vs))

def flist(fs):
    return "\n".join(f"{len(f)};{','.join(map(str,f))};" + ("," if i < len(fs)-1 else ";") for i, f in enumerate(fs))

def build_merged(block, scale=1.2, zoff=0.3):
    name, verts, faces, orig_mat, normals, fnorm = parse_mesh(block)
    nv = len(verts); fverts = [[float(a), float(b), float(c)] for a, b, c in verts]
    cx = (min(v[0] for v in fverts)+max(v[0] for v in fverts))/2
    cy = (min(v[1] for v in fverts)+max(v[1] for v in fverts))/2
    out_v = [[f"{cx+(v[0]-cx)*scale:.6f}", f"{cy+(v[1]-cy)*scale:.6f}", f"{v[2]+zoff:.6f}"] for v in fverts]
    all_v = verts + out_v
    all_f = faces + [[i+nv for i in f] for f in faces]
    face_idx = [0]*len(faces) + [1]*len(faces)
    fi_txt = "\n".join(f"  {v}," for v in face_idx[:-1]) + f"\n  {face_idx[-1]};;"
    all_n = normals + normals
    all_fn = fnorm + [[i+nv for i in f] for f in fnorm]
    return f"""Mesh {name} {{
 {len(all_v)};
{vlist(all_v)}
 {len(all_f)};
{flist(all_f)}
MeshMaterialList {{
 2;
 {len(face_idx)};
{fi_txt}
{orig_mat}
{OUT_MAT}
}}
 MeshNormals {{
 {len(all_n)};
{vlist(all_n)}
 {len(all_fn)};
{flist(all_fn)}
 }}
}}"""

def is_template(s, idx):
    j = idx-1
    while j > 0 and s[j] in " \t\r\n": j -= 1
    return j >= 7 and s[j-7:j+1] == "template"

for n in range(1, 10):
    fn = f"t_{n}.x"
    txt = open(f"{SRC}\\{fn}", encoding="latin-1").read()
    repls = []
    for m in re.finditer(r"\bMesh\s+\w+\s*\{", txt):
        if is_template(txt, m.start()): continue
        o = txt.index('{', m.start()); e = match_brace(txt, o)
        repls.append((m.start(), e+1, build_merged(txt[m.start():e+1])))
    for s, e, new in sorted(repls, key=lambda x: -x[0]):
        txt = txt[:s] + new + txt[e:]
    bal = txt.count('{') == txt.count('}')
    open(f"{DST}\\{fn}", "w", encoding="latin-1").write(txt)
    print(f"{fn}: mesh fuse={len(repls)}, graffe {'OK' if bal else 'KO'}")
