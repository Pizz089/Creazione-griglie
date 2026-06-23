import re

SRC = r"C:\Users\dario.STELNET\source\repos\Creazione griglie\_test_contorno\player_orig.X"
OUT = r"C:\Users\dario.STELNET\source\repos\Creazione griglie\_test_contorno\player.X"

txt = open(SRC, encoding="latin-1").read()

def match_brace(s, o):
    d = 0
    for i in range(o, len(s)):
        if s[i] == '{': d += 1
        elif s[i] == '}':
            d -= 1
            if d == 0: return i
    return -1

# Mesh sostitutiva: STESSO nome dello slot, ma rossa (face+emissive) e allungata in basso.
TEMPLATE = """Mesh {name} {{
 4;
{verts}
 2;
3;2,1,0;,
3;1,2,3;;
MeshMaterialList {{
 1;
 2;
  0,
  0;;
Material {{
 1.000000;0.000000;0.000000;1.000000;;
3.200000;
 0.000000;0.000000;0.000000;;
 1.000000;0.000000;0.000000;;
 }}
}}
 MeshNormals {{
 4;
0.000000;0.000000;-1.000000;,
0.000000;0.000000;-1.000000;,
0.000000;0.000000;-1.000000;,
0.000000;0.000000;-1.000000;;
 2;
3;2,1,0;,
3;1,2,3;;
 }}
}}"""

repls = []  # (start, end, newtext)
done = 0
for m in re.finditer(r"Frame\s+FR\d+_TOT\s*\{", txt):
    fopen = txt.index('{', m.start()); fend = match_brace(txt, fopen)
    frame = txt[fopen:fend]
    mm = re.search(r"Mesh\s+(\w+)\s*\{", frame)
    if not mm: continue
    name = mm.group(1)
    mstart = fopen + mm.start()
    mopen = fopen + frame.index('{', mm.start())
    mend = match_brace(txt, mopen)
    body = txt[mopen+1:mend]
    nums = re.match(r"\s*(\d+)\s*;", body)
    if not nums or int(nums.group(1)) != 4: continue
    verts = re.findall(r"(-?\d+\.\d+);(-?\d+\.\d+);(-?\d+\.\d+);", body[nums.end():])[:4]
    if len(verts) != 4: continue

    vv = [[float(x), float(y), float(z)] for (x, y, z) in verts]
    ymin = min(v[1] for v in vv)
    for v in vv:                       # spingo in basso i 2 vertici inferiori
        if abs(v[1] - ymin) < 0.01:
            v[1] = -45.0
    vblock = ";,\n".join(f"{x:.6f};{y:.6f};{z:.6f}" for x, y, z in vv) + ";;"
    repls.append((mstart, mend+1, TEMPLATE.format(name=name, verts=vblock)))
    done += 1

for s, e, new in sorted(repls, key=lambda t: -t[0]):
    txt = txt[:s] + new + txt[e:]

open(OUT, "w", encoding="latin-1").write(txt)
print(f"Slot TOT modificati (rosso + allungati): {done}")
print(f"Graffe: {txt.count('{')}/{txt.count('}')} -> {'OK' if txt.count('{')==txt.count('}') else 'SBILANCIATO'}")
print(f"_OUT presenti (devono essere 0): {txt.count('_OUT')}")
