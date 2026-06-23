import re
SRC = r"\\10.11.1.1\c$\Program Files (x86)\Steltronic\Vision\MediaNova\MeshBase\Styles\style#1\p10\player.X"
DST = r"\\10.11.1.1\c$\Program Files (x86)\Steltronic\Vision\MediaNova\MeshBase\Styles\style#15\p10\player.X"

def mb(s, o):
    d = 0
    for i in range(o, len(s)):
        d += 1 if s[i] == '{' else (-1 if s[i] == '}' else 0)
        if d == 0: return i
    return -1

# Nuovo placeholder: rettangolo interno (mat0=verde) + cornice esterna (mat1=magenta).
# 8 vertici (4 interni originali + 4 esterni 1.25x), 10 facce (2 interne mat0 + 8 cornice mat1).
def slot_mesh(name):
    return f"""Mesh {name} {{
 8;
-23.250000;11.625000;0.000000;,
-23.250000;-11.625000;0.000000;,
23.250000;11.625000;0.000000;,
23.250000;-11.625000;0.000000;,
-29.062500;14.531250;0.000000;,
-29.062500;-14.531250;0.000000;,
29.062500;14.531250;0.000000;,
29.062500;-14.531250;0.000000;;
 10;
3;2,1,0;,
3;1,2,3;,
3;0,4,6;,
3;0,6,2;,
3;1,5,7;,
3;1,7,3;,
3;0,4,5;,
3;0,5,1;,
3;2,6,7;,
3;2,7,3;;
MeshMaterialList {{
 2;
 10;
  0,
  0,
  1,
  1,
  1,
  1,
  1,
  1,
  1,
  1;;
Material {{
 0.000000;0.000000;0.000000;1.000000;;
0.000000;
 0.000000;0.000000;0.000000;;
 0.000000;1.000000;0.000000;;
 }}
Material {{
 0.000000;0.000000;0.000000;1.000000;;
0.000000;
 0.000000;0.000000;0.000000;;
 1.000000;0.000000;1.000000;;
 }}
}}
 MeshNormals {{
 8;
0.000000;0.000000;-1.000000;,
0.000000;0.000000;-1.000000;,
0.000000;0.000000;-1.000000;,
0.000000;0.000000;-1.000000;,
0.000000;0.000000;-1.000000;,
0.000000;0.000000;-1.000000;,
0.000000;0.000000;-1.000000;,
0.000000;0.000000;-1.000000;;
 10;
3;2,1,0;,
3;1,2,3;,
3;0,4,6;,
3;0,6,2;,
3;1,5,7;,
3;1,7,3;,
3;0,4,5;,
3;0,5,1;,
3;2,6,7;,
3;2,7,3;;
 }}
}}"""

txt = open(SRC, encoding="latin-1").read()
repls = []
for m in re.finditer(r"Frame\s+FR\d+_TOT\s*\{", txt):
    fo = txt.index('{', m.start()); fe = mb(txt, fo)
    mm = re.search(r"Mesh\s+(\w+)\s*\{", txt[fo:fe])
    name = mm.group(1)
    ms = fo + mm.start(); me = mb(txt, fo + txt[fo:fe].index('{', mm.start()))
    repls.append((ms, me + 1, slot_mesh(name)))
for a, b, new in sorted(repls, key=lambda x: -x[0]):
    txt = txt[:a] + new + txt[b:]
open(DST, "w", encoding="latin-1").write(txt)
print(f"Slot TOT con placeholder a bordo (2 materiali usati): {len(repls)}")
print(f"graffe: {txt.count('{')}/{txt.count('}')} -> {'OK' if txt.count('{') == txt.count('}') else 'KO'}")
