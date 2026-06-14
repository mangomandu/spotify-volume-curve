# Regenerates assets/curve.png — the "Make the whole slider usable" chart in the READMEs.
# Plots perceived loudness (= slider^(2.4*p)) for each preset, matching the in-app graph.
# Run from the repo root:  python3 assets/curve.py   (needs matplotlib + numpy + a Korean font)
import numpy as np
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib import font_manager

for fp in ["/mnt/c/Windows/Fonts/malgun.ttf", "/mnt/c/Windows/Fonts/malgunbd.ttf"]:
    font_manager.fontManager.addfont(fp)
plt.rcParams["font.family"] = "Malgun Gothic"
plt.rcParams["axes.unicode_minus"] = False

# ---- palette (matches the app) ----
BG      = "#0e0e15"
PANEL   = "#13131c"
GRID    = "#26263200"  # transparent-ish; we draw grid manually
GRIDC   = "#262630"
TEXT    = "#f2f2f5"
SUB     = "#9a9aa8"
GREEN   = "#1ed760"   # Spotify green — the recommended "Even" preset
AMBER   = "#e8b84b"   # Linear (loud early)
BLUE    = "#5b8def"   # Slight ramp
RED     = "#ff5a4d"   # Spotify default — the top-heavy problem we fix

FELT = 2.4  # Spotify applies ~x^4 on top of what we send, so perceived loudness ~= position^(2.4*p)

x = np.linspace(0, 1, 400)

fig = plt.figure(figsize=(10.5, 6.0), dpi=150)
fig.patch.set_facecolor(BG)
ax = fig.add_axes([0.085, 0.12, 0.885, 0.74])
ax.set_facecolor(PANEL)

# manual faint grid
for v in range(0, 101, 20):
    ax.axhline(v, color=GRIDC, lw=1, zorder=0)
    ax.axvline(v, color=GRIDC, lw=1, zorder=0)

# even reference (perfect diagonal)
ax.plot([0, 100], [0, 100], color="#3a3a46", lw=1.6, ls=(0, (5, 5)), zorder=1)
ax.text(97, 92, "even", color="#55555f", fontsize=10, ha="right", va="bottom", style="italic")

def felt(p):
    return np.power(x, FELT * p) * 100

# draw from least to most prominent
ax.plot(x*100, felt(1.0), color=RED,   lw=2.6, ls=(0, (6, 4)), zorder=3,
        label="스포티파이 디폴트 · Spotify default (p 1.0) — top-heavy")
ax.plot(x*100, felt(0.6), color=BLUE,  lw=2.2, zorder=4,
        label="살짝 쏠림 · Slight ramp (p 0.6)")
ax.plot(x*100, felt(0.3), color=AMBER, lw=2.2, zorder=4,
        label="리니어 · Linear (p 0.3) — loud early")
ax.plot(x*100, felt(0.4), color=GREEN, lw=3.4, zorder=6,
        label="고름 · Even (p 0.4)  ★ recommended")

# the punchline: at the half-way slider, what do you actually hear?
xs = 50
ax.axvline(xs, color="#3a3a46", lw=1.1, ls=(0, (2, 3)), zorder=2)

def marker(p, color, label, dx, dy, ha):
    yv = (xs/100) ** (FELT * p) * 100
    ax.scatter([xs], [yv], s=70, color=color, zorder=8, edgecolors=BG, linewidths=1.5)
    ax.annotate(label, (xs, yv), textcoords="offset points", xytext=(dx, dy),
                color=color, fontsize=12, fontweight="bold", ha=ha, va="center")

marker(0.4, GREEN, "you hear 51%", 14, 7, "left")
marker(1.0, RED,   "only 19%", 14, -3, "left")
ax.text(xs-2.5, 4, "← slider at the half-way point", color=SUB, fontsize=9.5, ha="right", va="bottom")

# titles
fig.text(0.085, 0.945, "Make the whole slider usable", color=TEXT, fontsize=21, fontweight="bold")
fig.text(0.085, 0.895, "What you actually hear — flatten Spotify's top-heavy curve so loudness tracks where you put the slider.",
         color=SUB, fontsize=11)

ax.set_xlim(0, 100); ax.set_ylim(0, 100)
ax.set_xlabel("Slider position  (%)", color=SUB, fontsize=12)
ax.set_ylabel("Perceived loudness  (%)", color=SUB, fontsize=12)
ax.set_xticks(range(0, 101, 20)); ax.set_yticks(range(0, 101, 20))
ax.tick_params(colors=SUB, labelsize=10)
for s in ax.spines.values():
    s.set_color("#2c2c38")

leg = ax.legend(loc="upper left", frameon=True, fontsize=10.5,
                facecolor="#16161f", edgecolor="#2c2c38", labelcolor=TEXT,
                borderpad=0.9, labelspacing=0.6)
leg.get_frame().set_alpha(0.95)

fig.savefig("assets/curve.png", facecolor=BG)
print("wrote assets/curve.png")
