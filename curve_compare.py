import numpy as np
import matplotlib.pyplot as plt

x = np.linspace(0, 1, 500)

# curve: actual_volume = slider_position ** p
curves = [
    (1.0,  "p=1.0  Linear  (= YouTube web)", "#2ca02c", "-",  2.6),
    (0.5,  "p=0.5  Balanced",                "#1f77b4", "-",  2.6),
    (0.35, "p=0.35 Strong",                  "#d62728", "-",  2.6),
    (2.0,  "p=2.0  (spicetify default = worse)", "#888888", "--", 1.6),
]

fig, (ax1, ax2) = plt.subplots(1, 2, figsize=(13, 5.4))

# ---- Panel 1: mapping ----
for p, label, c, ls, lw in curves:
    ax1.plot(x*100, (x**p)*100, color=c, ls=ls, lw=lw, label=label)

# mark slider = 50%
ax1.axvline(50, color="k", lw=0.8, alpha=0.3)
for p, _, c, _, _ in curves:
    yv = (0.5**p)*100
    ax1.plot(50, yv, "o", color=c, ms=6)
    ax1.annotate(f"{yv:.0f}%", (50, yv), textcoords="offset points",
                 xytext=(7, -2), fontsize=9, color=c, fontweight="bold")

ax1.set_title("Volume mapping:  actual = slider ^ p", fontsize=12, fontweight="bold")
ax1.set_xlabel("Slider position (%)")
ax1.set_ylabel("Actual volume sent (%)")
ax1.set_xlim(0, 100); ax1.set_ylim(0, 100)
ax1.grid(alpha=0.25); ax1.legend(loc="lower right", fontsize=9)
ax1.text(50, 4, "at 50% slider", fontsize=8, alpha=0.5, ha="center")

# ---- Panel 2: sensitivity (slope) ----
xs = np.linspace(0.001, 1, 500)
for p, label, c, ls, lw in curves:
    slope = p * xs**(p-1)            # d(volume)/d(slider), dimensionless
    ax2.plot(xs*100, slope, color=c, ls=ls, lw=lw, label=label)

ax2.axhline(1.0, color="#2ca02c", lw=0.8, alpha=0.4)
ax2.set_title("Sensitivity:  volume change per 1% slider move", fontsize=12, fontweight="bold")
ax2.set_xlabel("Slider position (%)")
ax2.set_ylabel("Sensitivity (steepness)")
ax2.set_xlim(0, 100); ax2.set_ylim(0, 4)
ax2.grid(alpha=0.25); ax2.legend(loc="upper right", fontsize=9)
ax2.annotate("p<1: gentle at TOP\n(tames the jumpiness)", (88, 0.45),
             fontsize=9, ha="center", color="#d62728")
ax2.annotate("p=2: jumpy at TOP\n(your original complaint)", (80, 3.2),
             fontsize=9, ha="center", color="#555555")

fig.suptitle("Spotify volume curve: linear vs power-curve (p)", fontsize=14, fontweight="bold")
fig.tight_layout(rect=[0, 0, 1, 0.96])
fig.savefig("/home/dlfnek/projects/spotify-linear-volume/curve_compare.png", dpi=130)
print("saved")

# also print a small table
print("\nslider% ->  actual volume %")
print("pos | p=1(lin) | p=0.5 | p=0.35 | p=2")
for s in [10,25,50,75,90,100]:
    f = s/100
    print(f"{s:3d} | {f*100:7.0f}  | {f**0.5*100:5.0f} | {f**0.35*100:6.0f} | {f**2*100:3.0f}")
