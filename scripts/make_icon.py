"""Draws the Servo icon (a stylised servo skull in front of a gear) and writes every size the project needs.

Run from the repository root:  python scripts/make_icon.py
Needs Pillow (pip install pillow). The generated files are committed, so this only has to be run to change the design.
"""
import math
from pathlib import Path

from PIL import Image, ImageDraw

SIZE = 1024
SCALE = 2  # drawn at twice the size and scaled down, for smooth edges

BACKGROUND = "#141922"
BRASS = "#c9a227"
BRASS_DARK = "#7d6224"
BONE = "#e9e0c9"
BONE_SHADE = "#c7bb9b"
DARK = "#10141b"
LENS = "#e0301f"

ROOT = Path(__file__).resolve().parent.parent
LAUNCHER_ASSETS = ROOT / "src" / "Servitor.Launcher" / "Assets"
APP_WWWROOT = ROOT / "src" / "Servitor.App" / "wwwroot"


def s(value):
    return value * SCALE


def box(left, top, right, bottom):
    return [s(left), s(top), s(right), s(bottom)]


def circle(draw, cx, cy, radius, fill):
    draw.ellipse(box(cx - radius, cy - radius, cx + radius, cy + radius), fill=fill)


def gear(draw, cx, cy, outer, inner, teeth, fill):
    points = []
    for tooth in range(teeth):
        base = 2 * math.pi * tooth / teeth
        step = 2 * math.pi / teeth
        for offset, radius in ((0.0, inner), (0.18, outer), (0.50, outer), (0.68, inner)):
            angle = base + offset * step
            points.append((s(cx + radius * math.cos(angle)), s(cy + radius * math.sin(angle))))
    draw.polygon(points, fill=fill)


def draw_icon():
    image = Image.new("RGBA", (s(SIZE), s(SIZE)), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)

    draw.rounded_rectangle(box(0, 0, SIZE, SIZE), radius=s(210), fill=BACKGROUND)
    draw.rounded_rectangle(box(22, 22, SIZE - 22, SIZE - 22), radius=s(190), outline=BRASS_DARK, width=s(10))

    # Gear behind the skull.
    gear(draw, 512, 530, 430, 380, 14, BRASS_DARK)
    circle(draw, 512, 530, 330, BACKGROUND)

    # Antenna on top of the skull.
    draw.line([s(650), s(235), s(730), s(95)], fill=BRASS, width=s(18))
    circle(draw, 735, 85, 34, LENS)
    circle(draw, 724, 74, 11, "#ffd9d2")

    # Jaw and cheek bones.
    draw.polygon([(s(330), s(560)), (s(694), s(560)), (s(668), s(705)), (s(606), s(770)), (s(418), s(770)), (s(356), s(705))], fill=BONE)
    draw.rounded_rectangle(box(396, 735, 628, 850), radius=s(30), fill=BONE_SHADE)

    # Cranium, with a brass plate over the upper left part of it.
    cranium = box(262, 190, 762, 670)
    draw.ellipse(cranium, fill=BONE)
    mask = Image.new("L", image.size, 0)
    ImageDraw.Draw(mask).ellipse(cranium, fill=255)
    plate = Image.new("RGBA", image.size, (0, 0, 0, 0))
    plate_draw = ImageDraw.Draw(plate)
    plate_draw.polygon([(s(240), s(250)), (s(610), s(140)), (s(540), s(300)), (s(240), s(420))], fill=BRASS)
    plate_draw.line([s(240), s(420), s(540), s(300), s(610), s(140)], fill=BRASS_DARK, width=s(10))
    for rivet in ((300, 300), (380, 262), (460, 232)):
        circle(plate_draw, rivet[0], rivet[1], 11, BRASS_DARK)
    image.paste(plate, (0, 0), Image.composite(plate.getchannel("A"), Image.new("L", image.size, 0), mask))

    # Eye sockets: an empty one and a mechanical eye.
    draw.ellipse(box(338, 388, 482, 552), fill=DARK)
    circle(draw, 614, 470, 90, BRASS)
    circle(draw, 614, 470, 68, DARK)
    circle(draw, 614, 470, 44, LENS)
    circle(draw, 598, 452, 13, "#ffd9d2")

    # Nose and teeth.
    draw.polygon([(s(512), s(545)), (s(478), s(625)), (s(546), s(625))], fill=DARK)
    for x in (448, 480, 512, 544, 576):
        draw.line([s(x), s(742), s(x), s(850)], fill=DARK, width=s(9))
    draw.line([s(404), s(742), s(620), s(742)], fill=DARK, width=s(9))

    return image.resize((SIZE, SIZE), Image.LANCZOS)


def main():
    icon = draw_icon()
    LAUNCHER_ASSETS.mkdir(parents=True, exist_ok=True)

    icon.save(LAUNCHER_ASSETS / "servo.png")
    icon.save(LAUNCHER_ASSETS / "servo.ico", sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])
    icon.save(LAUNCHER_ASSETS / "servo.icns")

    # The installable web app uses the same symbol.
    icon.resize((512, 512), Image.LANCZOS).save(APP_WWWROOT / "icon-512.png")
    icon.resize((192, 192), Image.LANCZOS).save(APP_WWWROOT / "icon-192.png")


if __name__ == "__main__":
    main()
