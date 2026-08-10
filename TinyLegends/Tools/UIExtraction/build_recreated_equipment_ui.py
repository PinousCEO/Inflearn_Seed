#!/usr/bin/env python3
"""Build reusable, text-free Equipment UI sprites for BrightTheme/Recreated."""
from __future__ import annotations

import json
from pathlib import Path

from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Assets/05_Resources/UI/BrightTheme/Recreated/Equipment"
SCALE = 3


def canvas(size, color=(0, 0, 0, 0)):
    return Image.new("RGBA", (size[0] * SCALE, size[1] * SCALE), color)


def save(image, relative):
    target = OUT / relative
    target.parent.mkdir(parents=True, exist_ok=True)
    if SCALE != 1 and image.width % SCALE == 0 and image.height % SCALE == 0:
        image = image.resize((image.width // SCALE, image.height // SCALE), Image.Resampling.LANCZOS)
    image.save(target, optimize=True)


def rr(draw, box, radius, **kwargs):
    draw.rounded_rectangle(tuple(v * SCALE for v in box), radius * SCALE, **kwargs)


def line(draw, points, fill, width=1):
    draw.line([(x * SCALE, y * SCALE) for x, y in points], fill=fill, width=width * SCALE)


def panel(name, light=False):
    im = canvas((128, 128))
    d = ImageDraw.Draw(im)
    if light:
        rr(d, (1, 1, 126, 126), 13, fill=(174, 174, 168, 255))
        rr(d, (3, 3, 124, 124), 11, fill=(72, 76, 78, 255))
        rr(d, (6, 6, 121, 121), 9, fill=(223, 224, 220, 255))
    else:
        rr(d, (1, 1, 126, 126), 13, fill=(104, 111, 111, 255))
        rr(d, (3, 3, 124, 124), 11, fill=(21, 31, 39, 255))
        rr(d, (6, 6, 121, 121), 8, fill=(27, 41, 51, 245))
    save(im, f"Frames/{name}.png")


def beveled_control(name, size, selected=False, pressed=False, disabled=False):
    im = canvas(size)
    d = ImageDraw.Draw(im)
    w, h = size
    outer = (184, 177, 159, 255)
    if disabled:
        face = (49, 56, 60, 255)
        outer = (104, 105, 101, 255)
    elif selected:
        face = (202, 79, 70, 255)
    elif pressed:
        face = (31, 47, 59, 255)
    else:
        face = (36, 54, 68, 255)
    rr(d, (1, 1, w - 2, h - 2), 10, fill=outer)
    rr(d, (3, 3, w - 4, h - 4), 8, fill=(35, 42, 47, 255))
    rr(d, (5, 5, w - 6, h - 6), 6, fill=face)
    save(im, f"Frames/{name}.png")


def slot(name, selected=False, armor=False):
    im = canvas((128, 128))
    d = ImageDraw.Draw(im)
    face = (47, 60, 61, 255) if armor else (58, 49, 70, 255)
    rr(d, (3, 3, 124, 124), 10, fill=(27, 35, 40, 255))
    if selected:
        rr(d, (4, 4, 123, 123), 9, fill=(255, 246, 225, 255))
        rr(d, (7, 7, 120, 120), 7, fill=(238, 91, 82, 255))
        rr(d, (10, 10, 117, 117), 6, fill=face)
        rr(d, (1, 1, 126, 126), 12, outline=(255, 111, 98, 150), width=2 * SCALE)
    else:
        rr(d, (4, 4, 123, 123), 9, fill=(174, 171, 157, 255))
        rr(d, (7, 7, 120, 120), 7, fill=(58, 68, 72, 255))
        rr(d, (10, 10, 117, 117), 6, fill=face)
    save(im, f"Frames/{name}.png")


def diamond(draw, cx, cy, radius, color, width=2):
    pts = [(cx, cy - radius), (cx + radius, cy), (cx, cy + radius), (cx - radius, cy), (cx, cy - radius)]
    line(draw, pts, color, width)
    if radius >= 7:
        diamond(draw, cx, cy, max(2, radius // 3), color, 1)


def decorations():
    # Full-width equipment-title rule. The reference uses a strong centre
    # ornament and lets both outer ends dissolve into the dark header.
    im = canvas((1024, 64))
    d = ImageDraw.Draw(im)
    centre = 512
    gap = 25
    fade_length = 210

    def smoothstep(value):
        value = max(0.0, min(1.0, value))
        return value * value * (3.0 - 2.0 * value)

    # Draw per-column so alpha and thickness both taper instead of ending on a
    # hard vertical cut. The two rails merge softly near the invisible ends.
    for x in range(0, centre-gap):
        strength = smoothstep(x / fade_length)
        alpha_main = int(220 * strength)
        alpha_soft = int(105 * strength)
        sx = x * SCALE
        d.line((sx, 46*SCALE, sx, 47*SCALE), fill=(196,184,162,alpha_main), width=SCALE)
        d.line((sx, 50*SCALE, sx, 50*SCALE), fill=(143,137,125,alpha_soft), width=SCALE)
    for x in range(centre+gap, 1024):
        strength = smoothstep((1023-x) / fade_length)
        alpha_main = int(220 * strength)
        alpha_soft = int(105 * strength)
        sx = x * SCALE
        d.line((sx, 46*SCALE, sx, 47*SCALE), fill=(196,184,162,alpha_main), width=SCALE)
        d.line((sx, 50*SCALE, sx, 50*SCALE), fill=(143,137,125,alpha_soft), width=SCALE)

    # Small tapered connectors lead into the centre jewel.
    line(d, ((centre-gap,46),(centre-12,46),(centre-5,50)), (205,193,170,235), 1)
    line(d, ((centre+gap,46),(centre+12,46),(centre+5,50)), (205,193,170,235), 1)
    diamond(d, centre, 48, 11, (214,202,179,255), 2)
    save(im, "Decorations/Title_Rule.png")

    im = canvas((32, 32)); d = ImageDraw.Draw(im)
    diamond(d, 16, 16, 8, (224, 216, 199, 255), 2)
    save(im, "Decorations/Title_Sparkle.png")

    im = canvas((40, 40)); d = ImageDraw.Draw(im)
    diamond(d, 20, 20, 11, (204, 191, 166, 255), 2)
    save(im, "Decorations/Title_Diamond.png")

    im = canvas((512, 32)); d = ImageDraw.Draw(im)
    line(d, ((0, 16), (239, 16)), (150, 151, 148, 180), 1)
    line(d, ((273, 16), (511, 16)), (150, 151, 148, 180), 1)
    diamond(d, 256, 16, 5, (150, 151, 148, 210), 1)
    save(im, "Decorations/Detail_Divider.png")

    im = canvas((512, 512)); d = ImageDraw.Draw(im)
    c = (137, 140, 139, 64)
    for radius in (82, 118):
        d.ellipse(((256-radius)*SCALE, (256-radius)*SCALE, (256+radius)*SCALE, (256+radius)*SCALE), outline=c, width=2*SCALE)
    diamond(d, 256, 256, 166, c, 2)
    for angle_pts in (
        ((256, 52), (256, 460)), ((52, 256), (460, 256)),
        ((112, 112), (400, 400)), ((400, 112), (112, 400)),
    ):
        line(d, angle_pts, c, 1)
    save(im, "Decorations/Paperdoll_Rune.png")

    im = canvas((384, 96)); d = ImageDraw.Draw(im)
    d.ellipse((20*SCALE, 25*SCALE, 364*SCALE, 84*SCALE), fill=(68, 78, 83, 55), outline=(121, 127, 128, 105), width=6*SCALE)
    d.ellipse((67*SCALE, 37*SCALE, 317*SCALE, 72*SCALE), outline=(191, 193, 188, 135), width=5*SCALE)
    save(im, "Decorations/Paperdoll_Pedestal.png")


def icons():
    im = canvas((64, 64)); d = ImageDraw.Draw(im)
    line(d, ((32, 16), (32, 48)), (224, 217, 202, 255), 5)
    line(d, ((16, 32), (48, 32)), (224, 217, 202, 255), 5)
    save(im, "Icons/Icon_Plus.png")

    im = canvas((64, 64)); d = ImageDraw.Draw(im)
    d.polygon([(16*SCALE, 23*SCALE), (48*SCALE, 23*SCALE), (32*SCALE, 43*SCALE)], fill=(224, 217, 202, 255))
    save(im, "Icons/Icon_Dropdown.png")

    im = canvas((64, 64)); d = ImageDraw.Draw(im)
    for y, length in ((18, 30), (31, 23), (44, 16)):
        line(d, ((25, y), (25+length, y)), (224, 217, 202, 255), 4)
    line(d, ((14, 15), (14, 47)), (224, 217, 202, 255), 4)
    d.polygon([(8*SCALE, 42*SCALE), (20*SCALE, 42*SCALE), (14*SCALE, 51*SCALE)], fill=(224, 217, 202, 255))
    save(im, "Icons/Icon_SortOrder.png")


def scrollbar_assets():
    # Stretch-safe recessed navy track with the same warm ivory outline used by
    # the equipment panels. It contains no fixed ornament in the stretch area.
    im = canvas((32, 128)); d = ImageDraw.Draw(im)
    rr(d, (4, 1, 27, 126), 11, fill=(157, 153, 141, 230))
    rr(d, (6, 3, 25, 124), 9, fill=(44, 53, 59, 255))
    rr(d, (9, 6, 22, 121), 6, fill=(25, 39, 49, 255))
    save(im, "Scrollbar/Scrollbar_Track.png")

    # Neutral handle: selected feedback comes from a restrained coral inner
    # edge, not from a bright modern mobile-scrollbar color.
    im = canvas((32, 72)); d = ImageDraw.Draw(im)
    rr(d, (2, 1, 29, 70), 11, fill=(215, 207, 190, 255))
    rr(d, (4, 3, 27, 68), 9, fill=(70, 76, 79, 255))
    rr(d, (7, 6, 24, 65), 7, fill=(177, 78, 70, 255))
    rr(d, (9, 8, 22, 63), 5, fill=(43, 59, 70, 255))
    save(im, "Scrollbar/Scrollbar_Handle.png")

    # Non-stretching centre grip, parented separately under the handle.
    im = canvas((24, 28)); d = ImageDraw.Draw(im)
    for cy in (8, 14, 20):
        diamond(d, 12, cy, 3, (221, 211, 191, 255), 1)
    save(im, "Scrollbar/Scrollbar_Grip.png")


def main():
    panel("Panel_Stats", light=False)
    panel("Panel_Inventory", light=False)
    panel("Panel_Paperdoll", light=True)
    panel("Panel_ItemDetail", light=True)
    beveled_control("Button_Action_Normal", (256, 80))
    beveled_control("Button_Action_Pressed", (256, 80), pressed=True)
    beveled_control("Button_Action_Disabled", (256, 80), disabled=True)
    beveled_control("Button_Icon_Normal", (80, 80))
    beveled_control("Button_Icon_Pressed", (80, 80), pressed=True)
    beveled_control("Equipment_Tab_Normal", (256, 72))
    beveled_control("Equipment_Tab_Selected", (256, 72), selected=True)
    beveled_control("CapacityBar", (256, 64))
    slot("Equipment_Slot_Normal")
    slot("Equipment_Slot_Armor", armor=True)
    slot("Equipment_Slot_Selected", selected=True)
    decorations()
    icons()
    scrollbar_assets()
    manifest = {
        "source_reference": "BrightReferences/Equipment_SevenKnights_Rebuilt.png",
        "usage": "Independent text-free sprites; compose icons, labels and item art in Unity.",
        "decorations": [
            "Decorations/Board_Ornament.png",
            "Decorations/Title_Rule.png",
            "Decorations/Title_Sparkle.png",
            "Decorations/Title_Diamond.png",
            "Decorations/Detail_Divider.png",
            "Decorations/Paperdoll_Rune.png",
            "Decorations/Paperdoll_Pedestal.png"
        ],
        "scrollbar": [
            "Scrollbar/Scrollbar_Track.png",
            "Scrollbar/Scrollbar_Handle.png",
            "Scrollbar/Scrollbar_Grip.png"
        ],
        "nine_slice_borders": {
            "Frames/Panel_Stats.png": [24, 24, 24, 24],
            "Frames/Panel_Inventory.png": [24, 24, 24, 24],
            "Frames/Panel_Paperdoll.png": [24, 24, 24, 24],
            "Frames/Panel_ItemDetail.png": [24, 24, 24, 24],
            "Frames/Button_Action_Normal.png": [24, 24, 24, 24],
            "Frames/Button_Action_Pressed.png": [24, 24, 24, 24],
            "Frames/Button_Action_Disabled.png": [24, 24, 24, 24],
            "Frames/Button_Icon_Normal.png": [20, 20, 20, 20],
            "Frames/Button_Icon_Pressed.png": [20, 20, 20, 20],
            "Frames/Equipment_Tab_Normal.png": [24, 24, 24, 24],
            "Frames/Equipment_Tab_Selected.png": [24, 24, 24, 24],
            "Frames/CapacityBar.png": [20, 20, 20, 20],
            "Frames/Equipment_Slot_Normal.png": [18, 18, 18, 18],
            "Frames/Equipment_Slot_Armor.png": [18, 18, 18, 18],
            "Frames/Equipment_Slot_Selected.png": [18, 18, 18, 18],
            "Scrollbar/Scrollbar_Track.png": [12, 12, 12, 12],
            "Scrollbar/Scrollbar_Handle.png": [12, 12, 12, 12]
        }
    }
    (OUT / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"Built Equipment UI under {OUT}")


if __name__ == "__main__":
    main()
