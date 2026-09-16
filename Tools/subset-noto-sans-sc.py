#!/usr/bin/env python3
"""
Rebuild Assets/HexRAssets/Asset/NotoSansSC-HexR-Subset.ttf.

The tutorial scenes carry bilingual labels, and no font that was already in this project
has a single CJK glyph -- TextMeshPro drew the Chinese half as empty boxes. Shipping the
whole of Noto Sans SC to fix that would add about 10 MB to every clone for the sake of
roughly 130 characters, so what is committed is a subset.

That makes this script load-bearing. If you add Chinese text to a scene and it renders as
boxes, the character is simply missing from the subset: add it to CJK_CHARS below, re-run,
and let Unity reimport the font.

    pip install fonttools
    python Tools/subset-noto-sans-sc.py

Source: Noto Sans SC, SIL Open Font License 1.1, from
https://github.com/google/fonts/tree/main/ofl/notosanssc  -- a variable font, instanced at
weight 400 before subsetting. The licence is copied next to the font as NotoSansSC-OFL.txt.

The family is renamed on the way through. Noto Sans SC descends from Adobe's Source Han
Sans and the licence declares the Reserved Font Name "Source"; renaming a modified version
keeps that question from arising, and it is honest that this is not the whole font.
"""

import os
import sys
import urllib.request

try:
    from fontTools.ttLib import TTFont
    from fontTools.varLib import instancer
    from fontTools import subset
except ImportError:
    sys.exit("fonttools is required:  pip install fonttools")

SOURCE_URL = (
    "https://github.com/google/fonts/raw/main/ofl/notosanssc/NotoSansSC%5Bwght%5D.ttf"
)
LICENCE_URL = "https://raw.githubusercontent.com/google/fonts/main/ofl/notosanssc/OFL.txt"

FAMILY = "Noto Sans SC HexR Subset"

_HERE = os.path.dirname(os.path.abspath(__file__))
OUT_DIR = os.path.join(_HERE, "..", "Assets", "HexRAssets", "Asset")
OUT_FONT = os.path.join(OUT_DIR, "NotoSansSC-HexR-Subset.ttf")
OUT_LICENCE = os.path.join(OUT_DIR, "NotoSansSC-OFL.txt")

# Every non-Latin character used by the tutorial's in-world labels.
CJK_CHARS = "。上下中事于互交亮人以件体你入到制动匙区医压反发取受可右合吗吸呼和喷嘴器在域基增学将左并式强心感所手扣扳把抓拟择指按挤振捏掌控握搏撞放方时显有本机板果模气水泉泡流滴演火灯点焰版物球理用由电碰示穿类紧置能脉脏腕自苹藏觉触谜跳过近选透通部钥钮钻附隐雨面颈题食馈（），？"


def charset():
    keep = set(CJK_CHARS)
    # Latin, digits and ASCII punctuation, plus CJK and fullwidth punctuation.
    for cp in (list(range(0x20, 0x7F))
               + list(range(0x3000, 0x3040))
               + list(range(0xFF00, 0xFFF0))):
        keep.add(chr(cp))
    return keep


def rename(font):
    """Retarget the name table at FAMILY, so the renamed family survives subsetting."""
    for rec in list(font["name"].names):
        if rec.nameID not in (1, 3, 4, 6, 16):
            continue
        text = rec.toUnicode().replace("Noto Sans SC", FAMILY)
        if rec.nameID == 6:  # PostScript name takes no spaces
            text = text.replace(" ", "")
        font["name"].setName(text, rec.nameID, rec.platformID, rec.platEncID, rec.langID)


def main():
    work = os.path.join(_HERE, "_fontwork")
    os.makedirs(work, exist_ok=True)
    src = os.path.join(work, "NotoSansSC-var.ttf")

    if not os.path.exists(src):
        print("downloading Noto Sans SC (about 17 MB) ...")
        urllib.request.urlretrieve(SOURCE_URL, src)
    urllib.request.urlretrieve(LICENCE_URL, OUT_LICENCE)

    font = TTFont(src)
    font = instancer.instantiateVariableFont(
        font, {"wght": 400}, inplace=True, updateFontNames=True)
    rename(font)

    static = os.path.join(work, "NotoSansSC-Regular.ttf")
    font.save(static)

    opts = subset.Options()
    opts.layout_features = ["*"]
    opts.name_IDs = ["*"]
    opts.name_legacy = True
    opts.notdef_outline = True
    opts.drop_tables = []

    f = subset.load_font(static, opts)
    subsetter = subset.Subsetter(options=opts)
    subsetter.populate(unicodes=[ord(c) for c in charset()])
    subsetter.subset(f)
    subset.save_font(f, OUT_FONT, opts)

    check = TTFont(OUT_FONT)
    cmap = check.getBestCmap()
    absent = [c for c in CJK_CHARS if ord(c) not in cmap]

    print("family : %s" % check["name"].getDebugName(1))
    print("glyphs : %d" % check["maxp"].numGlyphs)
    print("size   : %.0f KB" % (os.path.getsize(OUT_FONT) / 1024.0))
    print("missing: %d of %d tutorial characters" % (len(absent), len(CJK_CHARS)))
    if absent:
        sys.exit("subset is missing characters the tutorial uses")


if __name__ == "__main__":
    main()
