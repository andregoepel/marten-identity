#!/usr/bin/env python3
"""Vendor the AppFoundation web fonts into the RCL.

Downloads Manrope / Space Grotesk / JetBrains Mono from Google Fonts (latin +
latin-ext subsets — both cover German diacritics) and writes:

  src/AndreGoepel.Marten.Identity.Blazor/wwwroot/fonts/*.woff2
  src/AndreGoepel.Marten.Identity.Blazor/wwwroot/css/fonts.css

so the app never calls fonts.googleapis.com / fonts.gstatic.com at runtime
(GDPR compliance, offline use, reliability). All three are variable fonts, so a
single woff2 per family/subset serves the whole weight range.

Run from the repo root:  python scripts/fetch-fonts.py
"""
import os
import re
import urllib.request

# The same family/weight request the app used to load via <link>.
GOOGLE_CSS = (
    "https://fonts.googleapis.com/css2"
    "?family=Manrope:wght@400;500;600;700;800"
    "&family=Space+Grotesk:wght@400;500;600;700"
    "&family=JetBrains+Mono:wght@400;500"
    "&display=swap"
)
# A modern UA is required or Google serves legacy (ttf) instead of woff2.
UA = ("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
      "(KHTML, like Gecko) Chrome/120.0 Safari/537.36")

KEEP = ["latin", "latin-ext"]
SLUG = {"Manrope": "manrope", "Space Grotesk": "space-grotesk", "JetBrains Mono": "jetbrains-mono"}
ORDER = ["Manrope", "Space Grotesk", "JetBrains Mono"]

HERE = os.path.dirname(os.path.abspath(__file__))
WWWROOT = os.path.join(HERE, "..", "src", "AndreGoepel.Design.Blazor", "wwwroot")
FONTS_DIR = os.path.normpath(os.path.join(WWWROOT, "fonts"))
CSS_OUT = os.path.normpath(os.path.join(WWWROOT, "css", "fonts.css"))


def fetch(url: str) -> bytes:
    return urllib.request.urlopen(urllib.request.Request(url, headers={"User-Agent": UA})).read()


def main() -> None:
    css = fetch(GOOGLE_CSS).decode("utf-8")
    pat = re.compile(r"/\*\s*([\w-]+)\s*\*/\s*@font-face\s*\{(.*?)\}", re.S)

    faces: dict = {}  # (family, subset) -> merged face info
    for subset, body in pat.findall(css):
        if subset not in KEEP:
            continue
        fam = re.search(r"font-family:\s*'([^']+)'", body).group(1)
        weight = int(re.search(r"font-weight:\s*(\d+)", body).group(1))
        style = re.search(r"font-style:\s*(\w+)", body).group(1)
        url = re.search(r"url\((https://[^)]+\.woff2)\)", body).group(1)
        urange = re.search(r"unicode-range:\s*([^;]+);", body).group(1).strip()
        e = faces.setdefault((fam, subset), {"url": url, "style": style, "weights": [], "unicode": urange})
        e["weights"].append(weight)
        assert e["url"] == url, f"weight variants disagree on url for {(fam, subset)}"

    os.makedirs(FONTS_DIR, exist_ok=True)
    for stale in os.listdir(FONTS_DIR):
        if stale.endswith(".woff2"):
            os.remove(os.path.join(FONTS_DIR, stale))

    blocks = []
    for (fam, subset), e in sorted(faces.items(), key=lambda kv: (ORDER.index(kv[0][0]), KEEP.index(kv[0][1]))):
        fname = f"{SLUG[fam]}-{subset}.woff2"
        open(os.path.join(FONTS_DIR, fname), "wb").write(fetch(e["url"]))
        lo, hi = min(e["weights"]), max(e["weights"])
        wrange = f"{lo}" if lo == hi else f"{lo} {hi}"
        print(f"  {fname:28} weight {wrange}")
        blocks.append(
            f"/* {SLUG[fam]} — {subset} */\n"
            f"@font-face {{\n"
            f"  font-family: '{fam}';\n"
            f"  font-style: {e['style']};\n"
            f"  font-weight: {wrange};\n"
            f"  font-display: swap;\n"
            f"  src: url('../fonts/{fname}') format('woff2');\n"
            f"  unicode-range: {e['unicode']};\n"
            f"}}"
        )

    header = (
        "/*\n"
        " * Self-hosted AppFoundation fonts (Manrope / Space Grotesk / JetBrains Mono).\n"
        " *\n"
        " * Vendored from Google Fonts (latin + latin-ext subsets; both cover German\n"
        " * diacritics) so the app never calls fonts.googleapis.com / fonts.gstatic.com\n"
        " * at runtime — required for GDPR compliance, offline use, and reliability.\n"
        " * All three are variable fonts: one woff2 per family/subset serves the whole\n"
        " * weight range. Regenerate via scripts/fetch-fonts.py.\n"
        " */\n\n"
    )
    with open(CSS_OUT, "w", encoding="utf-8", newline="\n") as fo:
        fo.write(header + "\n\n".join(blocks) + "\n")
    print(f"Wrote {CSS_OUT}: {len(blocks)} @font-face blocks.")


if __name__ == "__main__":
    main()
