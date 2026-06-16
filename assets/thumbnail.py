# Regenerates assets/thumbnail.png — the README banner. Two mock cards mirror the app's
# two pillars: the loudness-curve volume panel, and the floating synced-lyrics window
# (album-tinted, one line highlighted). Run from the repo root:
#   python3 assets/thumbnail.py   (needs Pillow + numpy + Segoe UI fonts)
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter

S = 2                      # supersample factor
W, H = 1280 * S, 640 * S
FZ = "/mnt/c/Windows/Fonts/"

def font(name, px):
    return ImageFont.truetype(FZ + name, int(px * S))

f_title  = font("segoeuib.ttf", 110)
f_sub    = font("seguisb.ttf", 32)
f_desc   = font("segoeui.ttf", 21)
f_badge  = font("segoeui.ttf", 17)
f_ptitle = font("seguisb.ttf", 20)
f_axis   = font("segoeui.ttf", 13)
f_lyr    = font("segoeui.ttf", 16)
f_lyra   = font("seguisb.ttf", 17)
f_lhead  = font("seguisb.ttf", 16)
f_lsub   = font("segoeui.ttf", 12)

GREEN = (30, 215, 96)
WHITE = (242, 242, 245)
SUB   = (154, 154, 168)
DIM   = (150, 146, 158)

# ---- background: near-black with a soft glow behind the cards ----
yy, xx = np.mgrid[0:H, 0:W]
cx, cy = int(0.72 * W), int(0.42 * H)
d2 = ((xx - cx) / (0.58 * W)) ** 2 + ((yy - cy) / (0.58 * H)) ** 2
glow = np.clip(1 - d2, 0, 1) ** 1.6
base = np.array([14, 14, 21], dtype=float)
tint = np.array([26, 28, 40], dtype=float)
bg = base[None, None, :] + (tint - base)[None, None, :] * glow[:, :, None]
img = Image.fromarray(np.clip(bg, 0, 255).astype("uint8"), "RGB").convert("RGBA")

def shadow_for(box, blur, alpha, dy):
    sh = Image.new("RGBA", img.size, (0, 0, 0, 0))
    ImageDraw.Draw(sh).rounded_rectangle(
        [box[0], box[1] + dy, box[2], box[3] + dy], radius=18 * S, fill=(0, 0, 0, alpha))
    return sh.filter(ImageFilter.GaussianBlur(blur * S))

# ---------- right side: two floating cards ----------
VOL = [690 * S, 150 * S, 1000 * S, 408 * S]   # volume panel (landscape)
LYR = [1016 * S, 96 * S, 1200 * S, 556 * S]   # lyrics window (portrait)

img = Image.alpha_composite(img, shadow_for(LYR, 16, 150, 12 * S))
img = Image.alpha_composite(img, shadow_for(VOL, 14, 150, 10 * S))
d = ImageDraw.Draw(img)
def rr(box, r, **kw): d.rounded_rectangle(box, radius=int(r * S), **kw)

# ===== volume card =====
rr(VOL, 16, fill=(19, 19, 28, 255), outline=(38, 38, 48, 255), width=int(1.4 * S))
d.text((712 * S, 166 * S), "Spotify Volume", font=f_ptitle, fill=WHITE)
ccx, ccy, ca = 980 * S, 176 * S, 5 * S
d.line([(ccx - ca, ccy - ca), (ccx + ca, ccy + ca)], fill=(120, 120, 130), width=int(1.8 * S))
d.line([(ccx - ca, ccy + ca), (ccx + ca, ccy - ca)], fill=(120, 120, 130), width=int(1.8 * S))

GX0, GY0, GX1, GY1 = 712 * S, 220 * S, 978 * S, 386 * S
gw, gh = GX1 - GX0, GY1 - GY0
rr([GX0, GY0, GX1, GY1], 4, outline=(48, 48, 58, 255), width=int(1.2 * S))
def gx(t): return GX0 + t * gw
def gy(v): return GY1 - v * gh
def dashed(p0, p1, color, width, dash=10, gap=8):
    p0, p1 = np.array(p0, float), np.array(p1, float)
    L = np.hypot(*(p1 - p0)); u = (p1 - p0) / L; t = 0
    while t < L:
        a = p0 + u * t; b = p0 + u * min(t + dash * S, L)
        d.line([tuple(a), tuple(b)], fill=color, width=int(width * S)); t += (dash + gap) * S
dashed((GX0, GY1), (GX1, GY0), (58, 58, 70, 255), 1.6)
EXP = 0.6
pts = [(gx(t), gy(t ** EXP)) for t in np.linspace(0, 1, 240)]
d.line(pts, fill=GREEN, width=int(3.2 * S), joint="curve")
mt = 0.62; mx, my = gx(mt), gy(mt ** EXP)
dashed((mx, GY1), (mx, my), (110, 110, 120, 255), 1.4, dash=3, gap=4)
rdot = 6 * S
d.ellipse([mx - rdot, my - rdot, mx + rdot, my + rdot], fill=WHITE)
d.ellipse([mx - rdot, my - rdot, mx + rdot, my + rdot], outline=GREEN, width=int(2.2 * S))
d.text((GX0 - 2 * S, GY0 - 20 * S), "↑ loudness", font=f_axis, fill=(150, 150, 162))
sl = "slider →"; w = d.textlength(sl, font=f_axis)
d.text((GX1 - w, GY1 + 7 * S), sl, font=f_axis, fill=(150, 150, 162))

# ===== lyrics card (album-tinted) =====
rr(LYR, 16, fill=(40, 30, 58, 255), outline=(64, 52, 84, 255), width=int(1.4 * S))
lx = 1034 * S
d.text((lx, 112 * S), "Supersonic", font=f_lhead, fill=WHITE)
d.text((lx, 132 * S), "fromis_9", font=f_lsub, fill=(176, 168, 190))
# pin (tiny pushpin, pointing down) + close, top-right
pcx, pcy = 1166 * S, 120 * S
d.rounded_rectangle([pcx - 5 * S, pcy - 6 * S, pcx + 5 * S, pcy - 1 * S], radius=2 * S, fill=(210, 200, 220))
d.polygon([(pcx - 2.5 * S, pcy - 1 * S), (pcx + 2.5 * S, pcy - 1 * S), (pcx, pcy + 6 * S)], fill=(210, 200, 220))
xcx, xca = 1186 * S, 4.5 * S
d.line([(xcx - xca, pcy - xca), (xcx + xca, pcy + xca)], fill=(150, 140, 160), width=int(1.6 * S))
d.line([(xcx - xca, pcy + xca), (xcx + xca, pcy - xca)], fill=(150, 140, 160), width=int(1.6 * S))

lines = ["drying up", "my siren", "I like that", "day and night",
         "hit me up", "delight", "supersonic", "show me now"]
active = 3
ly = 174 * S
for i, t in enumerate(lines):
    if i == active:
        d.text((lx, ly - 1 * S), t, font=f_lyra, fill=WHITE)
    else:
        dist = abs(i - active)
        c = DIM if dist <= 1 else (118, 114, 126)
        d.text((lx, ly), t, font=f_lyr, fill=c)
    ly += 44 * S

# subtle transport row at the bottom of the lyrics card
ty = 528 * S; tcx = 1108 * S
d.polygon([(tcx - 30 * S, ty - 5 * S), (tcx - 30 * S, ty + 5 * S), (tcx - 37 * S, ty)], fill=(206, 200, 214))
d.rectangle([tcx - 39 * S, ty - 5 * S, tcx - 37 * S, ty + 5 * S], fill=(206, 200, 214))
d.ellipse([tcx - 12 * S, ty - 12 * S, tcx + 12 * S, ty + 12 * S], fill=WHITE)
d.polygon([(tcx - 3.5 * S, ty - 6 * S), (tcx - 3.5 * S, ty + 6 * S), (tcx + 6 * S, ty)], fill=(40, 30, 58))
d.polygon([(tcx + 30 * S, ty - 5 * S), (tcx + 30 * S, ty + 5 * S), (tcx + 37 * S, ty)], fill=(206, 200, 214))
d.rectangle([tcx + 37 * S, ty - 5 * S, tcx + 39 * S, ty + 5 * S], fill=(206, 200, 214))

# ---------- left content ----------
icon = Image.open("assets/icon.png").convert("RGBA").resize((74 * S, 74 * S), Image.LANCZOS)
img.alpha_composite(icon, (94 * S, 110 * S))
d = ImageDraw.Draw(img)
d.text((90 * S, 206 * S), "Volumify", font=f_title, fill=WHITE)
d.text((94 * S, 352 * S), "Two fixes for Spotify Desktop.", font=f_sub, fill=GREEN)
d.text((94 * S, 398 * S), "A volume curve that revives the dead bottom half,", font=f_desc, fill=SUB)
d.text((94 * S, 426 * S), "and floating synced lyrics that follow along.", font=f_desc, fill=SUB)

bx, by, bh = 94 * S, 472 * S, 34 * S
for label in ["Lossless-safe", "Synced lyrics", "No client patching", "EN · KR"]:
    tw = d.textlength(label, font=f_badge); bw = tw + 30 * S
    d.rounded_rectangle([bx, by, bx + bw, by + bh], radius=bh / 2,
                        fill=(28, 28, 36, 255), outline=(52, 52, 64, 255), width=max(1, int(1 * S)))
    d.text((bx + 15 * S, by + 7 * S), label, font=f_badge, fill=(200, 200, 210))
    bx += bw + 14 * S

img.convert("RGB").resize((1280, 640), Image.LANCZOS).save("assets/thumbnail.png")
print("wrote assets/thumbnail.png")
