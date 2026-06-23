import re
SRC = r"\\10.11.1.1\c$\Program Files (x86)\Steltronic\Vision\MediaNova\MeshBase\Styles\style#1\p10\player.X"
DST = r"\\10.11.1.1\c$\Program Files (x86)\Steltronic\Vision\MediaNova\MeshBase\Styles\style#15\p10\player.X"

def mb(s, o):
    d = 0
    for i in range(o, len(s)):
        d += 1 if s[i] == '{' else (-1 if s[i] == '}' else 0)
        if d == 0: return i
    return -1

# Slot con 2 materiali: 0 = VERDE (corpo cifra), 1 = MAGENTA (bordo).
NEW_MML = """MeshMaterialList {
 2;
 2;
  0,
  0;;
Material {
 0.000000;0.000000;0.000000;1.000000;;
0.000000;
 0.000000;0.000000;0.000000;;
 0.000000;1.000000;0.000000;;
 }
Material {
 0.000000;0.000000;0.000000;1.000000;;
0.000000;
 0.000000;0.000000;0.000000;;
 1.000000;0.000000;1.000000;;
 }
}"""

txt = open(SRC, encoding="latin-1").read()
repls = []
for m in re.finditer(r"Frame\s+FR\d+_TOT\s*\{", txt):
    fo = txt.index('{', m.start()); fe = mb(txt, fo)
    seg = txt[fo:fe]
    mml = seg.index("MeshMaterialList"); mmlo = seg.index('{', mml); mmle = mb(seg, mmlo)
    repls.append((fo + mml, fo + mmle + 1, NEW_MML))
for a, b, new in sorted(repls, key=lambda x: -x[0]):
    txt = txt[:a] + new + txt[b:]
open(DST, "w", encoding="latin-1").write(txt)
print(f"Slot TOT con 2 materiali (verde+magenta): {len(repls)}")
print(f"graffe: {txt.count('{')}/{txt.count('}')} -> {'OK' if txt.count('{') == txt.count('}') else 'KO'}")
