"""PVFX Foundry effect -> one of our VFX packs (the PixelLab "game" layout the importer reads).

PVFX Foundry 1.2 by nerijs, CC0 1.0 (https://nerijs.itch.io/pvfx-foundry). 희승 11-23: free effects "다운 받은다음에 그걸 기반으로
우리 게임에 맞게 좀 고치는게" - this turns a sheet into a pack; the colours can then go through the element ramp (VfxPlayer
recolor) or tools/aseprite/vfx_post.lua.

    python tools/pvfx/pvfx_to_pack.py <pvfx effect dir> <pack id> [--variant solar] [--tier lit|strict] [--scale 1]

Writes art/pixellab/vfx/<pack id>/game/{whole.png, manifest.json, SOURCE.txt}. Frame timing is kept exactly: frames are
repeated to a common step (the shortest frame), so the clip plays at 1000/step fps.
"""
import argparse, json, os
from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))

ap = argparse.ArgumentParser()
ap.add_argument("effect")
ap.add_argument("pack")
ap.add_argument("--variant")
ap.add_argument("--tier", default="lit")
ap.add_argument("--scale", type=int, default=1)
ap.add_argument("--ramp", help="five hex colours dark->light: every opaque pixel is repainted by brightness (our PixelRamp)")
a = ap.parse_args()
ramp = [tuple(int(h.strip("#")[i:i + 2], 16) for i in (0, 2, 4)) for h in a.ramp.split(",")] if a.ramp else None


def repaint(img):
    """Onto the game's five tones by brightness (strict tier: hard alpha), so a borrowed sheet speaks our palette."""
    px = img.load()
    for y in range(img.height):
        for x in range(img.width):
            r, g, b, al = px[x, y]
            if al == 0:
                continue
            l = (r * 0.3 + g * 0.59 + b * 0.11) / 255.0
            px[x, y] = ramp[min(4, int(l * 5))] + (al,)
    return img

src = os.path.join(a.effect, "variants", a.variant) if a.variant else a.effect
src = os.path.join(src, a.tier, "grid")
man = json.load(open(os.path.join(src, "manifest.json"), encoding="utf-8"))
sheet = Image.open(os.path.join(src, "sprite-sheet.png")).convert("RGBA")
W, H = man["source_size"]
px, py = man["pivot"]
frames = man["frames"]

ms = [f["duration"]["numerator_ms"] / f["duration"]["denominator"] for f in frames]
step = max(10.0, min(ms))
reps = [max(1, round(m / step)) for m in ms]

cells = []
for f, r in zip(frames, reps):
    s = f["sheet"]
    img = sheet.crop((s["x"], s["y"], s["x"] + s["width"], s["y"] + s["height"]))
    if img.size != (W, H):  # grid cells are the full canvas; guard anyway
        full = Image.new("RGBA", (W, H)); full.paste(img, (0, 0)); img = full
    if ramp:
        img = repaint(img.copy())
    if a.scale > 1:
        img = img.resize((W * a.scale, H * a.scale), Image.NEAREST)
    cells += [img] * r

n = len(cells)
fw, fh = W * a.scale, H * a.scale
cols = min(8, n)
rows = (n + cols - 1) // cols
out = Image.new("RGBA", (cols * fw, rows * fh))
for i, c in enumerate(cells):
    out.paste(c, ((i % cols) * fw, (i // cols) * fh))

dst = os.path.join(ROOT, "art", "pixellab", "vfx", a.pack, "game")
os.makedirs(dst, exist_ok=True)
out.save(os.path.join(dst, "whole.png"))
loop = man.get("loop_mode", "none") != "none"
fps = round(1000.0 / step, 3)
manifest = {
    "app": "PVFX Foundry 1.2 (converted by tools/pvfx/pvfx_to_pack.py)",
    "name": a.pack, "fps": fps, "frame_ms": step, "frames": n, "loop": loop,
    "canvas": {"w": fw, "h": fh},
    "anchor": {"kind": "origin", "x": px * a.scale, "y": py * a.scale},
    "clips": [{
        "name": "whole", "title": "the whole effect", "depth": "both", "loop": loop, "start_frame": 0, "frames": n,
        "frame": {"w": fw, "h": fh},
        "sheets": [{"file": "whole.png", "first_frame": 0, "frames": n, "columns": cols, "rows": rows}],
        "offset": {"x": -px * a.scale, "y": -py * a.scale},
    }],
}
json.dump(manifest, open(os.path.join(dst, "manifest.json"), "w", encoding="utf-8"), indent=1)
open(os.path.join(dst, "SOURCE.txt"), "w", encoding="utf-8").write(
    f"PVFX Foundry 1.2 by nerijs - CC0 1.0 - https://nerijs.itch.io/pvfx-foundry\n"
    f"effect: {man['program']}  variant: {a.variant or '-'}  tier: {a.tier}  scale: {a.scale}  ramp: {a.ramp or '-'}\n")
print(f"{a.pack}: {n} frames ({len(frames)} source) {fw}x{fh} @ {fps} fps, pivot {px},{py}, loop={loop}")
