import re

SRC = r"C:\Users\dario.STELNET\source\repos\Creazione griglie\_test_contorno\player_orig.X"
OUT = r"C:\Users\dario.STELNET\source\repos\Creazione griglie\_test_contorno\player.X"

txt = open(SRC, encoding="latin-1").read()

def match_brace(s, open_idx):
    depth = 0
    for i in range(open_idx, len(s)):
        if s[i] == '{': depth += 1
        elif s[i] == '}':
            depth -= 1
            if depth == 0: return i
    return -1

SCALE = 1.25      # copia piu' grande del 25%
ZOFF  = 0.5       # leggermente dietro al numero (verso la cella), davanti al box

TEMPLATE = """ Mesh {name}_OUT {{
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
 0.000000;0.000000;0.000000;1.000000;;
3.200000;
 0.000000;0.000000;0.000000;;
 1.000000;0.000000;1.000000;;
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
}}
"""

inserts = []
made = 0
for m in re.finditer(r"Frame\s+FR\d+_TOT\s*\{", txt):
    fopen = txt.index('{', m.start())
    fend  = match_brace(txt, fopen)
    frame = txt[fopen:fend]

    mm = re.search(r"Mesh\s+(\w+)\s*\{", frame)
    if not mm:
        continue
    name = mm.group(1)
    mopen = fopen + frame.index('{', mm.start())
    body  = txt[mopen+1:match_brace(txt, mopen)]

    nums = re.match(r"\s*(\d+)\s*;", body)
    if not nums or int(nums.group(1)) != 4:
        continue
    verts = re.findall(r"(-?\d+\.\d+);(-?\d+\.\d+);(-?\d+\.\d+);", body[nums.end():])[:4]
    if len(verts) != 4:
        continue

    scaled = [f"{float(x)*SCALE:.6f};{float(y)*SCALE:.6f};{float(z)+ZOFF:.6f}" for (x, y, z) in verts]
    vblock = ";,\n".join(scaled) + ";;"   # separatori virgola tra i vertici, ;; sull'ultimo

    block = TEMPLATE.format(name=name, verts=vblock)
    inserts.append((fend, "\n" + block))
    made += 1

for pos, text in sorted(inserts, key=lambda t: -t[0]):
    txt = txt[:pos] + text + txt[pos:]

open(OUT, "w", encoding="latin-1").write(txt)

print(f"Copie outline inserite: {made}")
print(f"Graffe: aperte={txt.count('{')} chiuse={txt.count('}')} -> {'OK' if txt.count('{')==txt.count('}') else 'SBILANCIATO!'}")
print(f"Mesh _OUT: {txt.count('_OUT')}")
