# Regenerates assets/overlay.png and assets/popup.png — the "See it" mockups.
# Clean renders of the overlay on Spotify's playbar (current white + green-ring knob), on a
# controlled dark background. Run from the repo root:  python3 assets/shots.py  (needs Pillow + Segoe UI)
import numpy as np
from PIL import Image, ImageDraw, ImageFont
import math

S = 3  # supersample
FZ = "/mnt/c/Windows/Fonts/"
def font(px): return ImageFont.truetype(FZ + "segoeui.ttf", int(px * S))

BG     = (0, 0, 0)
GREEN  = (30, 215, 96)
GREY   = (92, 92, 92)
ICON   = (185, 185, 185)
WHITE  = (255, 255, 255)
PANEL  = (40, 40, 40)
PANELB = (64, 64, 64)

def speaker(d, cx, cy, col, s=1.0):
    # body: small rect + triangle opening right
    w = 4 * s; h = 4 * s
    d.rectangle([cx - 9 * s, cy - h, cx - 4 * s, cy + h], fill=col)
    d.polygon([(cx - 4 * s, cy - h - 2 * s), (cx - 4 * s, cy + h + 2 * s), (cx + 2 * s, cy + 4 * s), (cx + 2 * s, cy - 4 * s)], fill=col)
    # two sound arcs
    for i, r in enumerate((5 * s, 9 * s)):
        bb = [cx + 1 * s - r + 4 * s, cy - r, cx + 1 * s + r + 4 * s, cy + r]
        d.arc(bb, -45, 45, fill=col, width=max(1, int(1.4 * S)))

def miniplayer(d, cx, cy, col):
    s = S
    d.rounded_rectangle([cx - 8 * s, cy - 6 * s, cx + 8 * s, cy + 6 * s], radius=2 * s, outline=col, width=max(1, int(1.4 * s)))
    d.rectangle([cx + 1 * s, cy + 0 * s, cx + 6 * s, cy + 4 * s], fill=col)

def fullscreen(d, cx, cy, col):
    s = S; a = 7 * s; L = 4 * s; w = max(1, int(1.6 * s))
    for sx, sy in ((-1, -1), (1, -1), (-1, 1), (1, 1)):
        x = cx + sx * a; y = cy + sy * a
        d.line([(x, y), (x - sx * L, y)], fill=col, width=w)
        d.line([(x, y), (x, y - sy * L)], fill=col, width=w)

def rail(d, x0, x1, cy, frac, h=3.0):
    hh = h * S
    kx = x0 + frac * (x1 - x0)
    d.rounded_rectangle([x0, cy - hh, x1, cy + hh], radius=hh, fill=GREY)          # unfilled track
    d.rounded_rectangle([x0, cy - hh, kx, cy + hh], radius=hh, fill=GREEN)          # green filled
    r = 7 * S
    d.ellipse([kx - r, cy - r, kx + r, cy + r], fill=WHITE)                          # white knob
    d.ellipse([kx - r, cy - r, kx + r, cy + r], outline=GREEN, width=max(1, int(2.0 * S)))  # green ring

def render(W, H, draw_fn, out):
    img = Image.new("RGB", (W * S, H * S), BG)
    d = ImageDraw.Draw(img)
    draw_fn(d)
    img.resize((W, H), Image.LANCZOS).save(out)
    print("wrote", out)

# ---------- overlay.png : a slice of Spotify's playbar with our green overlay on the rail ----------
def overlay(d):
    cy = 36 * S
    speaker(d, 22 * S, cy, ICON, s=S)
    rail(d, 48 * S, 250 * S, cy, 0.68)
    miniplayer(d, 286 * S, cy, ICON)
    fullscreen(d, 320 * S, cy, ICON)

render(348, 72, overlay, "assets/overlay.png")

# ---------- popup.png : the hover fly-out (roomy slider + %) above the same bar ----------
def popup(d):
    # bar strip along the bottom
    by = 150 * S
    speaker(d, 86 * S, by, ICON, s=S)
    rail(d, 112 * S, 250 * S, by, 0.62)
    miniplayer(d, 286 * S, by, ICON)
    fullscreen(d, 320 * S, by, ICON)
    # a couple of context icons far left (queue / devices)
    for i, x in enumerate((20, 48)):
        d.rounded_rectangle([x * S - 7 * S, by - 6 * S, x * S + 7 * S, by + 6 * S], radius=2 * S, outline=ICON, width=max(1, int(1.3 * S)))

    # fly-out panel above, centred over the rail
    px0, py0, px1, py1 = 70 * S, 16 * S, 300 * S, 96 * S
    d.rounded_rectangle([px0, py0, px1, py1], radius=12 * S, fill=PANEL, outline=PANELB, width=max(1, int(1.2 * S)))
    f = font(15)
    txt = "62%"
    tw = d.textlength(txt, font=f)
    d.text(((px0 + px1) / 2 - tw / 2, py0 + 12 * S), txt, font=f, fill=WHITE)
    rail(d, px0 + 26 * S, px1 - 26 * S, py1 - 24 * S, 0.62, h=3.4)

render(360, 188, popup, "assets/popup.png")
