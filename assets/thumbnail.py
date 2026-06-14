# Regenerates assets/thumbnail.png — the README banner. The mock panel mirrors the
# current app: title only (no logo square / status dot) over the simplified loudness graph.
# Run from the repo root:  python3 assets/thumbnail.py   (needs Pillow + numpy + Segoe UI fonts)
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter

S = 2                      # supersample factor
W, H = 1280 * S, 640 * S
FZ = "/mnt/c/Windows/Fonts/"

def font(name, px):
    return ImageFont.truetype(FZ + name, int(px * S))

f_title  = font("segoeuib.ttf", 116)
f_sub    = font("seguisb.ttf", 33)
f_desc   = font("segoeui.ttf", 22)
f_badge  = font("segoeui.ttf", 17)
f_ptitle = font("seguisb.ttf", 21)
f_axis   = font("segoeui.ttf", 14)
f_close  = font("segoeui.ttf", 20)

GREEN = (30, 215, 96)
WHITE = (242, 242, 245)
SUB   = (154, 154, 168)

# ---- background: near-black with a soft glow behind the panel ----
yy, xx = np.mgrid[0:H, 0:W]
cx, cy = int(0.76 * W), int(0.40 * H)
d2 = ((xx - cx) / (0.55 * W)) ** 2 + ((yy - cy) / (0.55 * H)) ** 2
glow = np.clip(1 - d2, 0, 1) ** 1.6
base = np.array([14, 14, 21], dtype=float)
tint = np.array([26, 28, 40], dtype=float)
bg = base[None, None, :] + (tint - base)[None, None, :] * glow[:, :, None]
img = Image.fromarray(np.clip(bg, 0, 255).astype("uint8"), "RGB").convert("RGBA")
d = ImageDraw.Draw(img)

def rr(box, r, **kw): d.rounded_rectangle(box, radius=int(r * S), **kw)

# ---- right panel (mock of the app window) ----
PX0, PY0, PX1, PY1 = 712 * S, 132 * S, 1194 * S, 508 * S
# soft shadow
shadow = Image.new("RGBA", img.size, (0, 0, 0, 0))
ds = ImageDraw.Draw(shadow)
ds.rounded_rectangle([PX0, PY0 + 10 * S, PX1, PY1 + 14 * S], radius=18 * S, fill=(0, 0, 0, 150))
shadow = shadow.filter(ImageFilter.GaussianBlur(14 * S))
img = Image.alpha_composite(img, shadow); d = ImageDraw.Draw(img)
def rr(box, r, **kw): d.rounded_rectangle(box, radius=int(r * S), **kw)
rr([PX0, PY0, PX1, PY1], 18, fill=(19, 19, 28, 255), outline=(38, 38, 48, 255), width=int(1.4 * S))

# panel header — title only (new app: no logo square, no status dot)
d.text((744 * S, 150 * S), "Spotify Volume", font=f_ptitle, fill=WHITE)
ccx, ccy, ca = 1166 * S, 161 * S, 5 * S  # close ✕, drawn as two strokes
d.line([(ccx - ca, ccy - ca), (ccx + ca, ccy + ca)], fill=(120, 120, 130), width=int(1.8 * S))
d.line([(ccx - ca, ccy + ca), (ccx + ca, ccy - ca)], fill=(120, 120, 130), width=int(1.8 * S))

# ---- graph inside the panel (matches the simplified in-app graph) ----
GX0, GY0, GX1, GY1 = 748 * S, 206 * S, 1162 * S, 452 * S
gw, gh = GX1 - GX0, GY1 - GY0
rr([GX0, GY0, GX1, GY1], 4, outline=(48, 48, 58, 255), width=int(1.2 * S))

def gx(t): return GX0 + t * gw
def gy(v): return GY1 - v * gh

# dashed "even" diagonal
def dashed(p0, p1, color, width, dash=10, gap=8):
    p0, p1 = np.array(p0, float), np.array(p1, float)
    L = np.hypot(*(p1 - p0)); u = (p1 - p0) / L; t = 0
    while t < L:
        a = p0 + u * t; b = p0 + u * min(t + dash * S, L)
        d.line([tuple(a), tuple(b)], fill=color, width=int(width * S))
        t += (dash + gap) * S
dashed((GX0, GY1), (GX1, GY0), (58, 58, 70, 255), 1.6)

# the loudness curve (a gentle bow — the app's perceptual curve)
EXP = 0.6
ts = np.linspace(0, 1, 240)
pts = [(gx(t), gy(t ** EXP)) for t in ts]
d.line(pts, fill=GREEN, width=int(3.4 * S), joint="curve")

# marker: vertical guide + white dot with a green ring (as in the app)
mt = 0.62; mv = mt ** EXP
mx, my = gx(mt), gy(mv)
dashed((mx, GY1), (mx, my), (110, 110, 120, 255), 1.4, dash=3, gap=4)
rdot = 6.5 * S
d.ellipse([mx - rdot, my - rdot, mx + rdot, my + rdot], fill=WHITE)
d.ellipse([mx - rdot, my - rdot, mx + rdot, my + rdot], outline=GREEN, width=int(2.2 * S))

# tiny axis labels
d.text((GX0 - 2 * S, (GY0 - 22 * S)), "↑ loudness", font=f_axis, fill=(150, 150, 162))
sl = "slider →"; w = d.textlength(sl, font=f_axis)
d.text((GX1 - w, GY1 + 8 * S), sl, font=f_axis, fill=(150, 150, 162))

# ---- left content ----
icon = Image.open("assets/icon.png").convert("RGBA").resize((76 * S, 76 * S), Image.LANCZOS)
img.alpha_composite(icon, (94 * S, 132 * S))
d.text((90 * S, 244 * S), "Volumify", font=f_title, fill=WHITE)
d.text((94 * S, 380 * S), "Finally, volume that makes sense.", font=f_sub, fill=GREEN)
d.text((94 * S, 420 * S), "No more dead bottom half or 80→100% cliff.", font=f_desc, fill=SUB)
d.text((94 * S, 450 * S), "Drives Spotify's real volume — syncs everywhere.", font=f_desc, fill=SUB)

# badges
bx, by, bh = 94 * S, 506 * S, 34 * S
for label in ["Lossless-safe", "Syncs to phone", "EN · KR"]:
    tw = d.textlength(label, font=f_badge)
    bw = tw + 32 * S
    d.rounded_rectangle([bx, by, bx + bw, by + bh], radius=bh / 2,
                        fill=(28, 28, 36, 255), outline=(52, 52, 64, 255), width=max(1, int(1 * S)))
    d.text((bx + 16 * S, by + 7 * S), label, font=f_badge, fill=(200, 200, 210))
    bx += bw + 16 * S

img.convert("RGB").resize((1280, 640), Image.LANCZOS).save("assets/thumbnail.png")
print("wrote assets/thumbnail.png")
