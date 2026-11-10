#!/usr/bin/env python3
"""Sticker geometry for the PMM_WeaponSkins preview.

1) extract: raw StickerMarkup text (tools/vmdl_markup.py output) -> tools/sticker_markup.json
       python3 gen_stickergeo.py extract _research/markup.json tools/sticker_markup.json
   vmdl_markup.py reads weapons/models/*/weapon_*.vmdl_c pulled from pak01 with tools/vpk_extract.py.

2) gen: tools/sticker_markup.json + tools/sticker_fit.json -> PMM_WeaponSkins/StickerGeo.g.cs
       python3 gen_stickergeo.py gen <PMM_WeaponSkins project folder (the one with the .csproj)>

sticker_fit.json holds, per weapon model, the affine map from sticker space to the 512x384 inventory render:
p = [scale, rotation deg, tx, ty, aspect, shear], mirror = 1 / -1, for the skin render ("skins") and for the plain
weapon render ("base") - the old silhouette fit, still used for Dual Berettas.
Projective data (all other weapons), from reference 3D renders (slot anchors and charm quads projected from the real models):
  "kab" = [k, a, b]: sticker space = model side view, u = a - k*x, v = b - k*z (fitted on the slot anchors, 1-2 px);
  "Hc"  = 3x3 homography model (x, z) -> 3D render px (fitted on all charm quad corners, 1-3 px median);
  "R"   = {"skins": 3x3, "base": 3x3} homography 3D render px -> 512x384 inventory render px (alpha registration). It was fitted by matching the markup Polygons (the weapon silhouette in sticker space)
against the render's alpha. Re-fit only if Valve re-renders the icons or changes a model.
"""
import json
import math
import os
import re
import sys

DEFS = {
    "weapon_pist_deagle": 1, "weapon_pist_elite": 2, "weapon_pist_fiveseven": 3, "weapon_pist_glock18": 4,
    "weapon_rif_ak47": 7, "weapon_rif_aug": 8, "weapon_snip_awp": 9, "weapon_rif_famas": 10,
    "weapon_snip_g3sg1": 11, "weapon_rif_galilar": 13, "weapon_mach_m249": 14, "weapon_rif_m4a4": 16,
    "weapon_smg_mac10": 17, "weapon_smg_p90": 19, "weapon_smg_mp5sd": 23, "weapon_smg_ump45": 24,
    "weapon_shot_xm1014": 25, "weapon_smg_bizon": 26, "weapon_shot_mag7": 27, "weapon_mach_negev": 28,
    "weapon_shot_sawedoff": 29, "weapon_pist_tec9": 30, "weapon_pist_taser": 31, "weapon_pist_hkp2000": 32,
    "weapon_smg_mp7": 33, "weapon_smg_mp9": 34, "weapon_shot_nova": 35, "weapon_pist_p250": 36,
    "weapon_snip_scar20": 38, "weapon_rif_sg556": 39, "weapon_snip_ssg08": 40, "weapon_rif_m4a1_silencer": 60,
    "weapon_pist_usp_silencer": 61, "weapon_pist_cz75a": 63, "weapon_pist_revolver": 64,
}


def parse_kv3(text):
    text = re.sub(r"<!--.*?-->", "", text, flags=re.S)
    tokens = re.findall(r'"(?:[^"\\]|\\.)*"|[\[\]{}=,]|[^\s\[\]{}=,]+', text)
    pos = 0

    def val():
        nonlocal pos
        t = tokens[pos]
        if t == "{":
            pos += 1
            obj = {}
            while tokens[pos] != "}":
                k = tokens[pos]
                pos += 2
                obj[k.strip('"')] = val()
            pos += 1
            return obj
        if t == "[":
            pos += 1
            arr = []
            while tokens[pos] != "]":
                if tokens[pos] == ",":
                    pos += 1
                    continue
                arr.append(val())
            pos += 1
            return arr
        pos += 1
        if t.startswith('"'):
            return t[1:-1]
        if t in ("true", "false"):
            return t == "true"
        try:
            return float(t)
        except ValueError:
            return t

    return val()


def extract(src, dst):
    raw = json.load(open(src, encoding="utf-8"))
    out = {}
    for model, text in sorted(raw.items()):
        if model not in DEFS or not text or text.startswith("ERROR"):
            continue
        kv = parse_kv3(text)
        hd = [e for e in kv.get("StickerMarkup", []) if e.get("Mesh") == "body_hd"]
        if not hd:
            continue
        slots = [[int(e["Index"]), round(e["Offset"][0], 4), round(e["Offset"][1], 4), round(e["Scale"], 4),
                  round(e.get("Rotation", 0.0), 4)] for e in hd]
        tris = []
        for poly in hd[0].get("Polygons", []):
            v = poly["Vertices"]
            tris += [round(x, 4) for x in v[: len(v) // 6 * 6]]
        # Charms: KeychainMarkup quads of the current model (LegacyModel absent). Corners are 4 x (x, y, z) in model
        # units, order top-left, top-right, bottom-left, bottom-right; x = towards the muzzle, y = side, z = up.
        quads, default = [], 0
        for q in kv.get("KeychainMarkup", []):
            if q.get("LegacyModel"):
                continue
            if q.get("DefaultLocation"):
                default = len(quads)
            quads.append([round(c, 4) for c in q["Corners"][:12]])
        out[model] = {"def": DEFS[model], "slots": slots, "tris": tris, "kc": quads, "kcdef": default}
    json.dump(out, open(dst, "w", encoding="utf-8"), separators=(",", ":"))
    print("extracted", len(out))


def matrix(part):
    s, rot, tx, ty, asp, sh = part["p"]
    m = part.get("mirror", 1)
    c, sn = math.cos(math.radians(rot)), math.sin(math.radians(rot))
    a = c * s * asp * m
    b = c * s * asp * sh - sn * s / asp
    d = sn * s * asp * m
    e = sn * s * asp * sh + c * s / asp
    return [a / 512, b / 512, tx / 512, d / 384, e / 384, ty / 384]


def f(x):
    return f"{x:.5f}f".replace("-0.00000f", "0f")


def parts_cs(parts):
    """Affine fits (old silhouette method) as projective parts with G = H = 0."""
    items = []
    for p in parts:
        mm = matrix(p)
        items.append("new(" + ", ".join(f(x) for x in mm) + f", 0f, 0f, {f(max(p.get('umin', -9), -9))}, {f(min(p.get('umax', 9), 9))})")
    return "new Part[] { " + ", ".join(items) + " }"


def mat3(h):
    return [[float(x) for x in row] for row in h]


def mul(a, b):
    return [[sum(a[i][k] * b[k][j] for k in range(3)) for j in range(3)] for i in range(3)]


def homo_cs(h):
    h = [[x / h[2][2] for x in row] for row in h]
    vals = [h[0][0], h[0][1], h[0][2], h[1][0], h[1][1], h[1][2], h[2][0], h[2][1]]
    return "new Part[] { new(" + ", ".join(f"{x:.7g}f" for x in vals) + ", -9f, 9f) }"


NORM = [[1 / 512, 0, 0], [0, 1 / 384, 0], [0, 0, 1]]


def sticker_homo(fit, imgset):
    """sticker (u, v) -> model (x, z) -> 3D render px (Hc) -> inventory render px (R) -> fraction."""
    k, a, b = fit["kab"]
    lm = [[-1 / k, 0, a / k], [0, -1 / k, b / k], [0, 0, 1]]
    return mul(NORM, mul(mat3(fit["R"][imgset]), mul(mat3(fit["Hc"]), lm)))


def charm_homo(fit, imgset):
    return mul(NORM, mul(mat3(fit["R"][imgset]), mat3(fit["Hc"])))


def gen(project):
    here = os.path.dirname(os.path.abspath(__file__))
    markup = json.load(open(os.path.join(here, "sticker_markup.json"), encoding="utf-8"))
    fits = json.load(open(os.path.join(here, "sticker_fit.json"), encoding="utf-8"))
    lines = [
        "// <auto-generated> tools/gen_stickergeo.py from the CS2 weapon models (StickerMarkup). Do not edit by hand.",
        "namespace PMM_WeaponSkins;",
        "",
        "internal static partial class StickerGeo",
        "{",
        "\tprivate static readonly Dictionary<int, Weapon> Data = new()",
        "\t{",
    ]
    for model, m in sorted(markup.items(), key=lambda kv: kv[1]["def"]):
        fit = fits.get(model)
        if not fit:
            print("no fit for", model)
            continue
        # every marked sticker spot (Index 0..5): slots 0..3 use their own, a schema (always on the 5th sticker) picks any
        by_index = {s[0]: s for s in m["slots"]}
        slots = []
        for k in range(max(4, max(by_index) + 1)):
            s = by_index.get(k) or m["slots"][min(k, len(m["slots"]) - 1)]
            slots += [s[1], s[2], 1.0 / s[3] if s[3] else 0.15]
        lines.append(f"\t\t// {model}")
        lines.append(f"\t\t[{m['def']}] = new(")
        lines.append("\t\t\tnew float[] { " + ", ".join(f(x) for x in slots) + " },")
        lines.append("\t\t\tnew float[] { " + ", ".join(f(x) for x in m["tris"]) + " },")
        projective = "kab" in fit and "Hc" in fit and "R" in fit
        if projective:
            lines.append("\t\t\t" + homo_cs(sticker_homo(fit, "skins")) + ",")
            lines.append("\t\t\t" + homo_cs(sticker_homo(fit, "base")) + ",")
        else:
            lines.append("\t\t\t" + parts_cs(fit["skins"]["parts"]) + ",")
            lines.append("\t\t\t" + parts_cs(fit["base"]["parts"]) + ",")
        kc = m.get("kc") or []
        if kc and projective:
            lines.append("\t\t\tnew float[] { " + ", ".join(f(x) for q in kc for x in q) + " },")
            lines.append(f"\t\t\t{m.get('kcdef', 0)},")
            lines.append("\t\t\t" + homo_cs(charm_homo(fit, "skins")) + ",")
            lines.append("\t\t\t" + homo_cs(charm_homo(fit, "base")) + f", {fit['kab'][0]:.6f}f),")
        else:
            lines.append("\t\t\tnull, 0, null, null, 0f),")
    lines += ["\t};", "}", ""]
    path = os.path.join(project, "StickerGeo.g.cs")
    with open(path, "w", encoding="utf-8", newline="\n") as fh:
        fh.write("\n".join(lines))
    print("wrote", path, len(markup), "weapons")


if __name__ == "__main__":
    if len(sys.argv) >= 4 and sys.argv[1] == "extract":
        extract(sys.argv[2], sys.argv[3])
    elif len(sys.argv) >= 3 and sys.argv[1] == "gen":
        gen(sys.argv[2])
    else:
        print(__doc__)
        sys.exit(1)
