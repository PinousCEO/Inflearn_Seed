#!/usr/bin/env python3
"""Restore artwork from the five approved references without style regeneration."""
import importlib.util
import json
import os
import shutil
import uuid
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Assets/05_Resources/UI/BrightTheme/Recreated"
BUILDER = ROOT / "Tools/UIExtraction/build_seven_knights_ui_assets.py"
HIGH_RES_COMMON = ROOT / "Tools/UIExtraction/Sources/Common_HighResSheet.png"
HIGH_RES_ORBS = ROOT / "Tools/UIExtraction/Sources/Main_Orbs_HighResSheet.png"
HIGH_RES_STAGE = ROOT / "Tools/UIExtraction/Sources/Main_Stage_HighResSheet.png"
CARTOON_POTIONS = ROOT / "Tools/UIExtraction/Sources/Main_Potions_CartoonSheet.png"
CATEGORY_EQUIPMENT = ROOT / "Tools/UIExtraction/Sources/Category_Equipment_HighRes.png"
CATEGORY_CONSUMABLES = ROOT / "Tools/UIExtraction/Sources/Category_Consumables_HighRes.png"
CATEGORY_MISC = ROOT / "Tools/UIExtraction/Sources/Category_Misc_HighRes.png"


def builder():
    spec = importlib.util.spec_from_file_location("builder", BUILDER)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def save(image, relative):
    path = OUT / relative
    path.parent.mkdir(parents=True, exist_ok=True)
    # Unity watches OUT continuously. Saving directly to the destination lets
    # TextureImporter open a partially-written PNG and produces intermittent
    # "File could not be read" / SourceAssetDB timestamp errors. Finish the
    # PNG beside its destination, then publish it with one atomic replacement.
    temporary = path.with_name(f".{path.name}.{uuid.uuid4().hex}.tmp")
    try:
        image.convert("RGBA").save(temporary, format="PNG", optimize=True)
        os.replace(temporary, path)
    finally:
        if temporary.exists():
            temporary.unlink()


def normalize(image, size, margin=8):
    image = image.convert("RGBA")
    bbox = image.getchannel("A").getbbox()
    if bbox:
        image = image.crop(bbox)
    image.thumbnail((size[0] - margin * 2, size[1] - margin * 2), Image.Resampling.LANCZOS)
    canvas = Image.new("RGBA", size, (0, 0, 0, 0))
    canvas.alpha_composite(image, ((size[0] - image.width) // 2, (size[1] - image.height) // 2))
    return canvas


def normalize_common(image, size=(64, 64), margin=2):
    """Fit a Common sprite to the canvas, enlarging small source crops too."""
    image = tight(image)
    max_size = (size[0] - margin * 2, size[1] - margin * 2)
    scale = min(max_size[0] / image.width, max_size[1] / image.height)
    image = image.resize(
        (max(1, round(image.width * scale)), max(1, round(image.height * scale))),
        Image.Resampling.LANCZOS,
    )
    canvas = Image.new("RGBA", size, (0, 0, 0, 0))
    canvas.alpha_composite(image, ((size[0] - image.width) // 2, (size[1] - image.height) // 2))
    return canvas


def export_high_res_common():
    """Export crisp 256px Common sprites from the approved high-res sheet."""
    sheet = Image.open(HIGH_RES_COMMON).convert("RGBA")
    names = [
        "Icon_Mail", "Icon_Settings", "Icon_Codex", "Icon_Gift",
        "Icon_Log", "Icon_Search", "Nav_Equipment", "Nav_Dungeon",
        "Nav_Main", "Nav_Skill", "Nav_Shop", "Badge_Notification",
    ]
    cell_w = sheet.width // 4
    cell_h = sheet.height // 3
    for index, name in enumerate(names):
        col, row = index % 4, index // 4
        cell = sheet.crop((col * cell_w, row * cell_h, (col + 1) * cell_w, (row + 1) * cell_h))
        cell = tight(cell)
        scale = min(240 / cell.width, 240 / cell.height)
        cell = cell.resize(
            (max(1, round(cell.width * scale)), max(1, round(cell.height * scale))),
            Image.Resampling.LANCZOS,
        )
        canvas = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
        canvas.alpha_composite(cell, ((256 - cell.width) // 2, (256 - cell.height) // 2))
        save(canvas, f"Common/{name}.png")

    # These four glyphs are reconstructed individually from their exact Main
    # reference crops; never replace them with the generic sheet variants.
    for name in ("Dungeon", "Main", "Skill", "Shop"):
        source = Image.open(ROOT / f"Tools/UIExtraction/Sources/Nav_{name}-HighRes.png").convert("RGBA")
        source = tight(source)
        scale = 480 / max(source.size)
        source = source.resize(
            (round(source.width * scale), round(source.height * scale)),
            Image.Resampling.LANCZOS,
        )
        canvas = Image.new("RGBA", (512, 512), (0, 0, 0, 0))
        canvas.alpha_composite(source, ((512 - source.width) // 2, (512 - source.height) // 2))
        save(canvas, f"Common/Nav_{name}.png")


def export_inventory_category_icons():
    """Create one coherent category set for Equipment, Consumables and Misc."""
    size = 256

    def fit(source):
        source = tight(source.convert("RGBA"))
        scale = 232 / max(source.size)
        source = source.resize(
            (max(1, round(source.width * scale)), max(1, round(source.height * scale))),
            Image.Resampling.LANCZOS,
        )
        result = Image.new("RGBA", (size, size), (0, 0, 0, 0))
        result.alpha_composite(source, ((size-source.width)//2, (size-source.height)//2))
        return result

    def common_ivory(source):
        """Preserve rendered bevels while mapping art to Common's ivory ramp."""
        source = source.convert("RGBA")
        pixels = source.load()
        for y in range(source.height):
            for x in range(source.width):
                red, green, blue, alpha = pixels[x, y]
                if not alpha:
                    continue
                lum = red * 0.24 + green * 0.56 + blue * 0.20
                if lum < 45:
                    color = (45, 49, 42)
                elif lum < 95:
                    color = (112, 98, 69)
                elif lum < 155:
                    color = (190, 166, 118)
                elif lum < 215:
                    color = (235, 224, 197)
                else:
                    color = (255, 250, 229)
                pixels[x, y] = (*color, alpha)
        return fit(source)

    equipment = fit(Image.open(CATEGORY_EQUIPMENT))
    # Render the other two at 4x instead of enlarging their old 96/128px art.
    # This keeps their edges as crisp as the high-resolution swords.
    k = 4
    potion = Image.new("RGBA", (size*k, size*k), (0, 0, 0, 0))
    d = ImageDraw.Draw(potion)
    def box(values): return tuple(v*k for v in values)
    d.ellipse(box((42, 65, 214, 240)), fill=(45, 49, 42, 255))
    d.polygon([(83*k, 103*k), (91*k, 48*k), (165*k, 48*k), (173*k, 103*k)], fill=(45, 49, 42, 255))
    d.ellipse(box((49, 72, 207, 233)), fill=(190, 166, 118, 255))
    d.polygon([(91*k, 106*k), (99*k, 55*k), (157*k, 55*k), (165*k, 106*k)], fill=(190, 166, 118, 255))
    d.ellipse(box((57, 80, 199, 225)), fill=(235, 224, 197, 255))
    d.polygon([(99*k, 109*k), (106*k, 62*k), (150*k, 62*k), (157*k, 109*k)], fill=(235, 224, 197, 255))
    d.ellipse(box((64, 115, 192, 219)), fill=(128, 112, 78, 255))
    d.ellipse(box((75, 91, 105, 142)), fill=(255, 250, 229, 230))
    d.rounded_rectangle(box((86, 35, 170, 68)), radius=9*k, fill=(45, 49, 42, 255))
    d.rounded_rectangle(box((93, 40, 163, 61)), radius=6*k, fill=(235, 224, 197, 255), outline=(190, 166, 118, 255), width=3*k)
    potion = potion.resize((size, size), Image.Resampling.LANCZOS)

    crystal = Image.new("RGBA", (size*k, size*k), (0, 0, 0, 0))
    d = ImageDraw.Draw(crystal)
    crystals = [
        [(128, 24), (178, 102), (158, 224), (98, 224), (78, 102)],
        [(49, 104), (101, 142), (92, 226), (39, 198), (20, 137)],
        [(207, 104), (236, 137), (217, 198), (164, 226), (155, 142)],
    ]
    for pts in crystals:
        p=[(x*k,y*k) for x,y in pts]
        d.polygon(p, fill=(45,49,42,255))
        cx=sum(x for x,y in pts)//len(pts)
        cy=sum(y for x,y in pts)//len(pts)
        inner=[((x*4+cx)//5*k,(y*4+cy)//5*k) for x,y in pts]
        d.polygon(inner, fill=(235,224,197,255))
        top=min(pts,key=lambda q:q[1]); bottom=max(pts,key=lambda q:q[1])
        d.polygon([(top[0]*k,top[1]*k),(cx*k,cy*k),(bottom[0]*k,bottom[1]*k)], fill=(190,166,118,255))
        d.line(p+[p[0]], fill=(112,98,69,255), width=3*k, joint="curve")
    crystal = crystal.resize((size, size), Image.Resampling.LANCZOS)
    # Final category masters are high-resolution renders matched directly to
    # the approved Common swords/settings style.
    potion = fit(Image.open(CATEGORY_CONSUMABLES))
    crystal = fit(Image.open(CATEGORY_MISC))
    save(equipment, "Common/Icon_Category_Equipment.png")
    save(potion, "Common/Icon_Category_Consumables.png")
    save(crystal, "Common/Icon_Category_Misc.png")


def fix_tab_state_dimensions():
    """Build selected from idle so both states have identical geometry."""
    idle_path = OUT / "Frames/Tab_Idle.png"
    idle = Image.open(idle_path).convert("RGBA")
    selected = idle.copy()
    pixels = selected.load()
    interior = idle.getchannel("A").filter(ImageFilter.MinFilter(3))
    interior_pixels = interior.load()
    for y in range(selected.height):
        for x in range(selected.width):
            red, green, blue, alpha = pixels[x, y]
            # Recolour the complete inset face, not a colour-selected subset;
            # this prevents unrecoloured dark texture pixels becoming dashes.
            # Only the one-pixel outer silhouette and full alpha geometry stay
            # untouched; the selected face reaches the same bottom edge.
            if alpha and interior_pixels[x, y] >= 250 and max(red, green, blue) < 110:
                lum = red * 0.22 + green * 0.50 + blue * 0.28
                pixels[x, y] = (
                    min(206, round(80 + lum * 2.25)),
                    min(92, round(28 + lum * 0.80)),
                    min(82, round(28 + lum * 0.68)),
                    alpha,
                )
    save(idle, "Frames/Tab_Idle.png")
    save(selected, "Frames/Tab_Selected.png")


def export_main_reference_hud(main_ref, b):
    """Export visible Main HUD composites and reference-derived layers."""
    hp_raw = main_ref.crop((25, 1317, 171, 1463)).convert("RGBA")
    mp_raw = main_ref.crop((771, 1317, 917, 1463)).convert("RGBA")
    stage_raw = main_ref.crop((306, 207, 631, 233)).convert("RGBA")

    def circular_cutout(source, radius=66):
        image = source.copy().convert("RGBA")
        alpha = Image.new("L", image.size, 0)
        ap = alpha.load()
        cx = cy = 72.5
        for y in range(image.height):
            for x in range(image.width):
                distance = ((x - cx) ** 2 + (y - cy) ** 2) ** 0.5
                if distance <= radius:
                    ap[x, y] = 255 if distance <= radius - 1.5 else max(0, round((radius - distance) / 1.5 * 255))
        image.putalpha(alpha)
        pixels = image.load()
        for y in range(image.height):
            for x in range(image.width):
                red, green, blue, opacity = pixels[x, y]
                if opacity and green > red * 1.18 and green > blue * 1.25:
                    pixels[x, y] = (red, green, blue, 0)
        return image

    hp = circular_cutout(hp_raw)
    mp = circular_cutout(mp_raw)
    stage = tight(b.flood_remove_background(stage_raw, tolerance=34))
    save(hp, "Main/Orb_HP_Composite.png")
    save(mp, "Main/Orb_MP_Composite.png")
    save(stage, "Main/Stage_Progress_Composite.png")

    # The visible ring is genuinely separable from the screenshot. Preserve
    # its pixels and clear only the center where dynamic fill/text belongs.
    frame = hp.copy()
    frame_alpha = frame.getchannel("A")
    fp = frame_alpha.load()
    cx = cy = 72.5
    for y in range(frame.height):
        for x in range(frame.width):
            distance = ((x - cx) ** 2 + (y - cy) ** 2) ** 0.5
            if distance < 51 or distance > 66:
                fp[x, y] = 0
    frame.putalpha(frame_alpha)
    frame = remove_reference_green(frame)
    save(frame, "Sliders/Orb_Frame.png")

    def orb_fill(source):
        # A broad blur removes the baked numbers while retaining the exact
        # reference hue, radial shading, rim light and non-flat tonal depth.
        image = Image.new("RGBA", source.size, (0, 0, 0, 0))
        inner = source.crop((21, 21, 125, 125)).convert("RGBA").filter(ImageFilter.GaussianBlur(18))
        image.alpha_composite(inner, (21, 21))
        alpha = Image.new("L", image.size, 0)
        ap = alpha.load()
        for y in range(image.height):
            for x in range(image.width):
                distance = ((x - cx) ** 2 + (y - cy) ** 2) ** 0.5
                if distance <= 52:
                    ap[x, y] = 255 if distance <= 50.5 else max(0, round((52 - distance) / 1.5 * 255))
        image.putalpha(alpha)
        return image

    hp_fill = orb_fill(hp_raw)
    mp_fill = orb_fill(mp_raw)
    save(hp_fill, "Sliders/Orb_HP_Fill.png")
    save(mp_fill, "Sliders/Orb_MP_Fill.png")
    background = hp_fill.convert("RGBA")
    bp = background.load()
    for y in range(background.height):
        for x in range(background.width):
            red, green, blue, alpha = bp[x, y]
            if alpha:
                luminance = round(red * 0.18 + green * 0.28 + blue * 0.10)
                bp[x, y] = (max(8, luminance // 2), max(14, luminance), max(18, luminance + 7), alpha)
    save(background, "Sliders/Orb_Background.png")

    # Split the visible progress control into usable same-size layers. The
    # composite above remains the pixel-authoritative reference.
    stage_frame = Image.new("RGBA", stage.size, (0, 0, 0, 0))
    stage_fill = Image.new("RGBA", stage.size, (0, 0, 0, 0))
    stage_background = Image.new("RGBA", stage.size, (0, 0, 0, 0))
    source_pixels = stage.load()
    frame_pixels = stage_frame.load()
    fill_pixels = stage_fill.load()
    background_pixels = stage_background.load()
    for y in range(stage.height):
        track_sample = source_pixels[min(150, stage.width - 1), y]
        for x in range(stage.width):
            pixel = source_pixels[x, y]
            if x < 22 or x >= stage.width - 22:
                frame_pixels[x, y] = pixel
            if x <= 108:
                red, green, blue, alpha = pixel
                if alpha and red + green + blue > 260:
                    fill_pixels[x, y] = pixel
            if 12 <= x < stage.width - 12 and track_sample[3]:
                background_pixels[x, y] = track_sample
    save(stage_frame, "Sliders/Stage_Frame.png")
    save(stage_background, "Sliders/Stage_Background.png")
    save(stage_fill, "Sliders/Stage_Fill.png")


def export_remade_main_hud():
    """Export aligned, high-resolution Unity layers remade in Main's style."""
    orb_sheet = Image.open(HIGH_RES_ORBS).convert("RGBA")
    cell_w, cell_h = orb_sheet.width // 2, orb_sheet.height // 2

    def orb_cell(col, row, visual_size, inner_only=False):
        cell = orb_sheet.crop((col * cell_w, row * cell_h, (col + 1) * cell_w, (row + 1) * cell_h))
        if inner_only:
            cx, cy, radius = cell.width // 2, cell.height // 2, round(min(cell.size) * 0.30)
            cell = cell.crop((cx - radius, cy - radius, cx + radius, cy + radius))
            mask = Image.new("L", cell.size, 0)
            ImageDraw.Draw(mask).ellipse((1, 1, cell.width - 2, cell.height - 2), fill=255)
            cell.putalpha(mask)
        else:
            cell = tight(cell)
        scale = visual_size / max(cell.size)
        cell = cell.resize((round(cell.width * scale), round(cell.height * scale)), Image.Resampling.LANCZOS)
        canvas = Image.new("RGBA", (512, 512), (0, 0, 0, 0))
        canvas.alpha_composite(cell, ((512 - cell.width) // 2, (512 - cell.height) // 2))
        return canvas

    orb_background = orb_cell(0, 0, 420, inner_only=True)
    orb_hp = orb_cell(1, 0, 420)
    orb_mp = orb_cell(0, 1, 420)
    orb_frame = orb_cell(1, 1, 480)
    save(orb_background, "Sliders/Orb_Background.png")
    save(orb_hp, "Sliders/Orb_HP_Fill.png")
    save(orb_mp, "Sliders/Orb_MP_Fill.png")
    save(orb_frame, "Sliders/Orb_Frame.png")

    hp_preview = Image.alpha_composite(Image.alpha_composite(orb_background, orb_hp), orb_frame)
    mp_preview = Image.alpha_composite(Image.alpha_composite(orb_background, orb_mp), orb_frame)
    save(hp_preview, "Main/Orb_HP_Preview.png")
    save(mp_preview, "Main/Orb_MP_Preview.png")

    # Draw each slider state as one continuous closed silhouette. Diamonds and
    # track are never composited as separate pieces, so no doubled outlines or
    # visible joins can occur at either 0% or 100%.
    scale = 4
    size = (1024 * scale, 128 * scale)
    left_diamond = [(16, 64), (52, 28), (88, 64), (52, 100)]
    right_diamond = [(936, 64), (972, 28), (1008, 64), (972, 100)]
    left_diamond = [(x * scale, y * scale) for x, y in left_diamond]
    right_diamond = [(x * scale, y * scale) for x, y in right_diamond]
    track = (52 * scale, 50 * scale, 972 * scale, 78 * scale)

    def unified_stage(full):
        image = Image.new("RGBA", size, (0, 0, 0, 0))
        mask = Image.new("L", size, 0)
        mask_draw = ImageDraw.Draw(mask)
        mask_draw.rectangle(track, fill=255)
        mask_draw.polygon(left_diamond, fill=255)
        mask_draw.polygon(right_diamond, fill=255)
        gradient = Image.new("RGBA", size, (0, 0, 0, 0))
        gp = gradient.load()
        for y in range(size[1]):
            t = y / max(1, size[1] - 1)
            if full:
                top, bottom = (248, 230, 177), (190, 151, 66)
            else:
                top, bottom = (48, 59, 54), (18, 28, 27)
            color = tuple(round(top[i] * (1 - t) + bottom[i] * t) for i in range(3))
            for x in range(size[0]):
                gp[x, y] = (*color, 255)
        image.paste(gradient, (0, 0), mask)
        draw = ImageDraw.Draw(image)
        outline = (91, 79, 48, 255) if not full else (119, 96, 48, 255)
        highlight = (167, 157, 118, 220) if not full else (255, 246, 205, 235)
        # No vertical end caps: the reference track flows directly into the
        # inner diamond points without creating a stray rectangle.
        draw.line((52 * scale, 50 * scale, 972 * scale, 50 * scale), fill=outline, width=3 * scale)
        draw.line((52 * scale, 78 * scale, 972 * scale, 78 * scale), fill=outline, width=3 * scale)
        # Repaint both diamonds over the overlapping track. This hides the
        # track lines inside them while keeping a broad, gap-free connection.
        diamond_mask = Image.new("L", size, 0)
        diamond_draw = ImageDraw.Draw(diamond_mask)
        diamond_draw.polygon(left_diamond, fill=255)
        diamond_draw.polygon(right_diamond, fill=255)
        image.paste(gradient, (0, 0), diamond_mask)
        draw = ImageDraw.Draw(image)
        # Keep the track-facing diamond edges open, exactly like the reference.
        # Left: top -> outer-left -> bottom. Right: top -> outer-right -> bottom.
        draw.line((left_diamond[1], left_diamond[0], left_diamond[3]), fill=outline, width=4 * scale, joint="curve")
        draw.line((right_diamond[1], right_diamond[2], right_diamond[3]), fill=outline, width=4 * scale, joint="curve")
        draw.line((88 * scale, 54 * scale, 936 * scale, 54 * scale), fill=highlight, width=1 * scale)
        draw.line((52 * scale, 34 * scale, 22 * scale, 64 * scale), fill=highlight, width=1 * scale)
        draw.line((972 * scale, 34 * scale, 1002 * scale, 64 * scale), fill=highlight, width=1 * scale)
        return image.resize((1024, 128), Image.Resampling.LANCZOS)

    stage_background = unified_stage(False)
    stage_fill = unified_stage(True)
    # Both layers must share the exact same coverage mask. Rendering the two
    # color variants independently can otherwise produce sub-pixel edge drift.
    shared_alpha = stage_background.getchannel("A")
    stage_fill.putalpha(shared_alpha)

    save(stage_background, "Sliders/Stage_Background.png")
    save(stage_fill, "Sliders/Stage_Fill.png")
    save(stage_fill, "Main/Stage_Progress_Preview.png")


def export_cartoon_potions():
    """Export the deliberately low-detail Main-style HP/MP potion pair."""
    sheet = Image.open(CARTOON_POTIONS).convert("RGBA")
    midpoint = sheet.width // 2
    for name, box in (
        ("HP", (0, 0, midpoint - 2, sheet.height)),
        ("MP", (midpoint + 2, 0, sheet.width, sheet.height)),
    ):
        art = sheet.crop(box)
        pixels = art.load()
        border = 8
        for y in range(art.height):
            for x in range(art.width):
                if x < border or y < border or x >= art.width - border or y >= art.height - border:
                    red, green, blue, _ = pixels[x, y]
                    pixels[x, y] = (red, green, blue, 0)
        art = tight(art)
        art.thumbnail((84, 84), Image.Resampling.LANCZOS)
        canvas = Image.new("RGBA", (96, 96), (0, 0, 0, 0))
        canvas.alpha_composite(art, ((96 - art.width) // 2, (96 - art.height) // 2))
        save(canvas, f"Main/Potion_{name}.png")


def export_reference_style_potions():
    """Keep the approved silhouette and flatten it to the reference's cartoon rendering."""
    for name in ("HP", "MP"):
        source = Image.open(OUT / f"Main/Potion_{name}.png").convert("RGBA")
        alpha = source.getchannel("A")
        # A small fixed palette removes the over-rendered gradients while
        # retaining the clean silhouette and antialiased alpha of the better
        # previous version.
        flattened = source.convert("RGB").quantize(
            colors=14,
            method=Image.Quantize.MEDIANCUT,
            dither=Image.Dither.NONE,
        ).convert("RGBA")
        flattened.putalpha(alpha)
        save(flattened, f"Main/Potion_{name}_ReferenceStyle.png")


def apply_stage_plate_alpha():
    """Make the top-center stage plate translucent like the Main reference."""
    path = OUT / "Main/Hud_Chip.png"
    if not path.exists():
        return
    image = Image.open(path).convert("RGBA")
    pixels = image.load()
    for y in range(image.height):
        for x in range(image.width):
            red, green, blue, alpha = pixels[x, y]
            # Preserve the bright metal frame, but let the green playfield
            # read through the dark central plate as it does in the reference.
            if alpha and max(red, green, blue) < 105:
                pixels[x, y] = (red, green, blue, min(alpha, 188))
            elif alpha:
                # Undo the earlier blanket 208 cap on the metal without
                # destroying antialiased edge coverage.
                pixels[x, y] = (red, green, blue, min(255, round(alpha * 255 / 208)))
    save(image, "Main/Hud_Chip.png")


def tight(image):
    image = image.convert("RGBA")
    bbox = image.getchannel("A").getbbox()
    return image.crop(bbox) if bbox else image


def upscale_to_fit(image, max_size):
    image = tight(image)
    scale = min(max_size[0] / image.width, max_size[1] / image.height)
    return image.resize(
        (max(1, round(image.width * scale)), max(1, round(image.height * scale))),
        Image.Resampling.LANCZOS,
    )


def integer_scale(image, factor=4):
    image = tight(image)
    return image.resize(
        (image.width * factor, image.height * factor),
        Image.Resampling.NEAREST,
    )


def remove_green_fringe(image):
    image = image.convert("RGBA")
    pixels = image.load()
    for y in range(image.height):
        for x in range(image.width):
            red, green, blue, alpha = pixels[x, y]
            if not alpha:
                continue
            base = max(red, blue)
            if green > red * 1.05 and green > blue * 1.10 and green - min(red, blue) > 8:
                # Keep the edge opacity/detail, neutralize only the green spill.
                neutral = max(red, blue)
                pixels[x, y] = (red, neutral, blue, alpha)
    return tight(image)


def remove_reference_green(image):
    """Remove the Main reference's green field, including enclosed holes."""
    image = image.convert("RGBA")
    pixels = image.load()
    for y in range(image.height):
        for x in range(image.width):
            red, green, blue, alpha = pixels[x, y]
            if not alpha:
                continue
            # The reference field is consistently green-dominant. Unlike the
            # edge flood, applying this test everywhere also clears the closed
            # centers of the gear and book glyphs.
            dominance = green - max(red, blue)
            if green > red * 1.12 and green > blue * 1.20 and dominance > 7:
                pixels[x, y] = (red, green, blue, 0)
    return image


def apply_shape_mask(image, kind):
    image = image.convert("RGBA")
    w, h = image.size
    scale = 4
    mask_large = Image.new("L", (w * scale, h * scale), 0)
    draw = ImageDraw.Draw(mask_large)
    if kind == "circle":
        inset = 8 * scale
        draw.ellipse((inset, inset, w * scale - inset - 1, h * scale - inset - 1), fill=255)
    else:
        points = [
            (5, 30), (27, 4), (w - 28, 4), (w - 5, 30),
            (w - 26, h - 15), (w // 2, h - 1), (25, h - 15),
        ]
        draw.polygon([(x * scale, y * scale) for x, y in points], fill=255)
    mask = mask_large.resize((w, h), Image.Resampling.LANCZOS)
    alpha = image.getchannel("A")
    image.putalpha(Image.composite(alpha, Image.new("L", (w, h), 0), mask))
    return tight(image)


def currency_gold_icon(size=128, supersample=4):
    s = size * supersample
    k = supersample
    image = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    center = s // 2
    # Layered painted rings reproduce the reference's compact cartoon coin.
    rings = [
        (60, (105, 62, 15, 255)),
        (58, (221, 151, 35, 255)),
        (52, (255, 207, 83, 255)),
        (47, (176, 104, 20, 255)),
        (43, (235, 169, 49, 255)),
        (35, (194, 122, 24, 255)),
    ]
    for radius, color in rings:
        r = radius * k
        draw.ellipse((center - r, center - r, center + r, center + r), fill=color)
    draw.arc((14*k, 14*k, 114*k, 114*k), 205, 330, fill=(116, 68, 14, 255), width=3*k)
    draw.arc((17*k, 16*k, 111*k, 110*k), 20, 165, fill=(255, 230, 124, 255), width=3*k)
    # Same four-point fantasy emblem, drawn as embossed gold rather than a new mark.
    star = [(64, 31), (73, 54), (98, 64), (73, 73),
            (64, 98), (55, 73), (31, 64), (55, 54)]
    star = [(x*k, y*k) for x, y in star]
    draw.polygon([(x+2*k, y+3*k) for x, y in star], fill=(121, 70, 14, 210))
    draw.polygon(star, fill=(255, 218, 104, 255), outline=(143, 83, 15, 255), width=2*k)
    draw.line(((64*k, 32*k), (64*k, 95*k)), fill=(255, 239, 156, 230), width=2*k)
    draw.line(((32*k, 64*k), (95*k, 64*k)), fill=(209, 136, 29, 230), width=2*k)
    return image.resize((size, size), Image.Resampling.LANCZOS)


def currency_gem_icon(size=128, supersample=4):
    s = size * supersample
    k = supersample
    image = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    outer = [(18, 40), (38, 18), (91, 18), (111, 40), (91, 100), (64, 116), (37, 100)]
    outer = [(x*k, y*k) for x, y in outer]
    draw.polygon([(x+2*k, y+3*k) for x, y in outer], fill=(8, 45, 113, 180))
    draw.polygon(outer, fill=(21, 111, 224, 255), outline=(6, 46, 126, 255), width=3*k)
    top = (64*k, 49*k)
    facets = [
        ([(18,40),(38,18),(64,49)], (72, 174, 255, 255)),
        ([(38,18),(91,18),(64,49)], (42, 139, 247, 255)),
        ([(91,18),(111,40),(64,49)], (18, 93, 206, 255)),
        ([(18,40),(37,100),(64,49)], (16, 87, 192, 255)),
        ([(37,100),(64,116),(64,49)], (12, 67, 169, 255)),
        ([(64,49),(64,116),(91,100)], (25, 112, 225, 255)),
        ([(64,49),(91,100),(111,40)], (12, 74, 180, 255)),
    ]
    for points, color in facets:
        draw.polygon([(x*k, y*k) for x, y in points], fill=color)
    draw.line(outer + [outer[0]], fill=(5, 42, 112, 255), width=3*k, joint="curve")
    draw.line(((39*k, 22*k), (60*k, 48*k)), fill=(170, 225, 255, 220), width=3*k)
    return tight(image.resize((size, size), Image.Resampling.LANCZOS))


def keep_largest_component(image):
    image = image.convert("RGBA")
    alpha = image.getchannel("A")
    w, h = image.size
    seen = set()
    groups = []
    for sy in range(h):
        for sx in range(w):
            if (sx, sy) in seen or alpha.getpixel((sx, sy)) < 24:
                continue
            stack = [(sx, sy)]
            seen.add((sx, sy))
            group = []
            while stack:
                x, y = stack.pop()
                group.append((x, y))
                for point in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
                    if 0 <= point[0] < w and 0 <= point[1] < h and point not in seen and alpha.getpixel(point) >= 24:
                        seen.add(point)
                        stack.append(point)
            groups.append(group)
    if not groups:
        return image
    keep = set(max(groups, key=len))
    pixels = image.load()
    for y in range(h):
        for x in range(w):
            if (x, y) not in keep:
                pixels[x, y] = (*pixels[x, y][:3], 0)
    return image


def remove_small_components(image, min_pixels=40):
    image = image.convert("RGBA")
    alpha = image.getchannel("A")
    w, h = image.size
    seen = set()
    groups = []
    for sy in range(h):
        for sx in range(w):
            if (sx, sy) in seen or alpha.getpixel((sx, sy)) < 24:
                continue
            stack = [(sx, sy)]
            seen.add((sx, sy))
            group = []
            while stack:
                x, y = stack.pop()
                group.append((x, y))
                for point in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
                    if 0 <= point[0] < w and 0 <= point[1] < h and point not in seen and alpha.getpixel(point) >= 24:
                        seen.add(point)
                        stack.append(point)
            groups.append(group)
    pixels = image.load()
    for group in groups:
        if len(group) < min_pixels:
            for x, y in group:
                pixels[x, y] = (*pixels[x, y][:3], 0)
    return image


def restore_enclosed_alpha(image):
    """Restore dark artwork pixels that color-key removal mistook for background."""
    image = image.convert("RGBA")
    alpha = image.getchannel("A")
    w, h = image.size
    exterior = set()
    stack = []
    for x in range(w):
        stack.extend(((x, 0), (x, h - 1)))
    for y in range(h):
        stack.extend(((0, y), (w - 1, y)))
    while stack:
        x, y = stack.pop()
        if (x, y) in exterior or alpha.getpixel((x, y)) >= 24:
            continue
        exterior.add((x, y))
        for point in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
            if 0 <= point[0] < w and 0 <= point[1] < h:
                stack.append(point)
    pixels = image.load()
    for y in range(h):
        for x in range(w):
            if alpha.getpixel((x, y)) < 24 and (x, y) not in exterior:
                pixels[x, y] = (*pixels[x, y][:3], 255)
    return image


def panel(size, radius=18):
    image = Image.new("RGBA", size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    draw.rounded_rectangle((1, 1, size[0] - 2, size[1] - 2), radius, fill=(25, 38, 47, 238), outline=(95, 104, 102, 255), width=2)
    return image


def profile_backplate(size=(304, 96), radius=14):
    """Rebuild the reference's left HUD plate without a fake right capsule."""
    w, h = size
    image = Image.new("RGBA", size, (0, 0, 0, 0))
    pixels = image.load()
    fade_start = 226
    for y in range(h):
        for x in range(w):
            # Rounded left corners; the right side deliberately fades away.
            if x < radius and y < radius:
                if (x - radius) ** 2 + (y - radius) ** 2 > radius ** 2:
                    continue
            if x < radius and y > h - 1 - radius:
                if (x - radius) ** 2 + (y - (h - 1 - radius)) ** 2 > radius ** 2:
                    continue
            fade = 1.0 if x < fade_start else max(
                0.025, (w - 1 - x) / (w - 1 - fade_start)
            )
            pixels[x, y] = (8, 17, 20, round(164 * fade))
    draw = ImageDraw.Draw(image)
    border = (194, 200, 176, 255)
    draw.line((radius, 0, fade_start - 1, 0), fill=border, width=2)
    draw.line((radius, h - 2, fade_start - 1, h - 2), fill=border, width=2)
    for x in range(fade_start, w):
        fade = max(0.025, (w - 1 - x) / (w - 1 - fade_start))
        faded_border = (*border[:3], max(1, round(border[3] * fade)))
        draw.point((x, 0), fill=faded_border)
        draw.point((x, 1), fill=faded_border)
        draw.point((x, h - 2), fill=faded_border)
        draw.point((x, h - 1), fill=faded_border)
    draw.arc((0, 0, radius * 2, radius * 2), 180, 270, fill=border, width=2)
    draw.arc((0, h - 1 - radius * 2, radius * 2, h - 1), 90, 180, fill=border, width=2)
    draw.line((0, radius, 0, h - 1 - radius), fill=border, width=2)
    return image


def currency_backplate(size=(380, 62), radius=18):
    image = Image.new("RGBA", size, (0, 0, 0, 0))
    w, h = size
    fade_end = 100
    pixels = image.load()
    for y in range(h):
        for x in range(w):
            if x > w - 1 - radius and y < radius:
                if (x - (w - 1 - radius)) ** 2 + (y - radius) ** 2 > radius ** 2:
                    continue
            if x > w - 1 - radius and y > h - 1 - radius:
                if (x - (w - 1 - radius)) ** 2 + (y - (h - 1 - radius)) ** 2 > radius ** 2:
                    continue
            fade = max(0.025, min(1.0, x / fade_end))
            pixels[x, y] = (8, 17, 20, round(164 * fade))

    draw = ImageDraw.Draw(image)
    border = (194, 200, 176, 255)
    for x in range(w - radius):
        fade = max(0.025, min(1.0, x / fade_end))
        faded_border = (*border[:3], max(1, round(border[3] * fade)))
        draw.point((x, 0), fill=faded_border)
        draw.point((x, 1), fill=faded_border)
        draw.point((x, h - 2), fill=faded_border)
        draw.point((x, h - 1), fill=faded_border)
    draw.arc((w - 1 - radius * 2, 0, w - 1, radius * 2), 270, 360, fill=border, width=2)
    draw.arc((w - 1 - radius * 2, h - 1 - radius * 2, w - 1, h - 1), 0, 90, fill=border, width=2)
    draw.line((w - 2, radius, w - 2, h - 1 - radius), fill=border, width=2)
    return image


def mail_icon(size=(96, 72), supersample=4):
    w, h = size
    scale = supersample
    image = Image.new("RGBA", (w * scale, h * scale), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    outer = [(3, 6), (w - 4, 6), (w - 4, h - 7), (3, h - 7)]
    outer = [(x * scale, y * scale) for x, y in outer]
    fill = (230, 222, 185, 255)
    line = (65, 69, 55, 255)
    fold = (91, 91, 68, 255)
    draw.rounded_rectangle(
        (outer[0][0], outer[0][1], outer[2][0], outer[2][1]),
        radius=4 * scale,
        fill=fill,
        outline=line,
        width=2 * scale,
    )
    left = (4 * scale, 8 * scale)
    right = ((w - 5) * scale, 8 * scale)
    center = ((w // 2) * scale, (h // 2 + 3) * scale)
    draw.line((left, center, right), fill=fold, width=2 * scale, joint="curve")
    draw.line(
        ((4 * scale, (h - 9) * scale), ((w // 2 - 13) * scale, (h // 2 + 1) * scale)),
        fill=fold,
        width=2 * scale,
    )
    draw.line(
        (((w - 5) * scale, (h - 9) * scale), ((w // 2 + 13) * scale, (h // 2 + 1) * scale)),
        fill=fold,
        width=2 * scale,
    )
    return image.resize(size, Image.Resampling.LANCZOS)


def add_unity_safe_padding(image, padding=4):
    image = image.convert("RGBA")
    canvas = Image.new(
        "RGBA",
        (image.width + padding * 2, image.height + padding * 2),
        (0, 0, 0, 0),
    )
    canvas.alpha_composite(image, (padding, padding))
    return canvas


def ensure_unity_safe_padding(image, padding=4):
    image = image.convert("RGBA")
    bbox = image.getchannel("A").getbbox()
    if not bbox:
        return image
    left = max(0, padding - bbox[0])
    top = max(0, padding - bbox[1])
    right = max(0, padding - (image.width - bbox[2]))
    bottom = max(0, padding - (image.height - bbox[3]))
    if not any((left, top, right, bottom)):
        return image
    canvas = Image.new(
        "RGBA",
        (image.width + left + right, image.height + top + bottom),
        (0, 0, 0, 0),
    )
    canvas.alpha_composite(image, (left, top))
    return canvas


def restore_panel_bottom(image, band=26):
    """Rebuild clipped lower artwork from the intact upper border/corners."""
    image = tight(image.convert("RGBA"))
    band = min(band, image.height // 3)
    restored = image.copy()
    corner = band
    # Continue the existing face texture downward using the immediately
    # preceding band; this avoids mirrored seams and stretched vertical lines.
    face = image.crop((corner + 1, image.height - band * 2, image.width - corner - 1, image.height - band))
    restored.paste(face, (corner + 1, image.height - band))
    top_border = image.crop((0, 0, image.width, band)).transpose(Image.Transpose.FLIP_TOP_BOTTOM)
    border_mask = Image.new("L", (image.width, band), 0)
    mask_draw = ImageDraw.Draw(border_mask)
    mask_draw.rectangle((0, 0, corner, band), fill=255)
    mask_draw.rectangle((image.width - corner - 1, 0, image.width, band), fill=255)
    mask_draw.rectangle((0, band - 8, image.width, band), fill=255)
    restored.paste(top_border, (0, image.height - band), border_mask)
    return restored


def main():
    b = builder()
    main_ref = b.load("Main_SevenKnights_Rebuilt.png")
    shop_ref = b.load("Shop_SevenKnights_Rebuilt.png")
    skill_ref = b.load("Skill_SevenKnights_Rebuilt.png")
    dungeon_ref = b.load("Dungeon_SevenKnights_Rebuilt.png")
    equipment_ref = b.load("Equipment_SevenKnights_Rebuilt.png")

    # Exact reference utility/navigation glyphs.
    common = {
        "Icon_Codex": (800, 108, 845, 151),
        "Icon_Settings": (873, 108, 916, 151),
        "Icon_Gift": (38, 255, 67, 287),
        "Icon_Log": (39, 333, 68, 365),
    }
    for name, box in common.items():
        art = b.flood_remove_background(main_ref.crop(box), tolerance=40)
        if name in {"Icon_Codex", "Icon_Settings"}:
            art = remove_reference_green(art)
        art = keep_largest_component(art)
        save(normalize_common(art), f"Common/{name}.png")

    nav_crops = {
        "Equipment": (82, 1518, 125, 1563),
        "Dungeon": (258, 1517, 307, 1564),
        "Skill": (628, 1517, 677, 1564),
        "Shop": (812, 1517, 861, 1565),
    }
    for name, box in nav_crops.items():
        glyph = b.flood_remove_background(main_ref.crop(box), tolerance=40)
        save(normalize_common(keep_largest_component(glyph)), f"Common/Nav_{name}.png")

    # Main is selected in Main.png, so take its clean unselected glyph from
    # Equipment.png instead of trying to erase the red selected plate.
    nav_main = b.flood_remove_background(equipment_ref.crop((438, 1514, 496, 1566)), tolerance=40)
    save(normalize_common(keep_largest_component(nav_main)), "Common/Nav_Main.png")

    # Keep the actual reference envelope. The badge is a separate component,
    # so selecting the largest alpha component removes it without redrawing
    # or changing the envelope silhouette.
    mail_source = main_ref.crop((722, 108, 773, 151)).convert("RGBA")
    # The notification badge overlaps the envelope in the reference. Restore
    # the hidden corner from the envelope's symmetric left edge; the badge is
    # exported and positioned separately in Unity.
    original_mail = mail_source.copy()
    # The envelope itself is symmetric. Rebuild the whole occluded half from
    # the clean half so no badge rim or detached stroke can survive.
    for y in range(mail_source.height):
        for x in range(26, mail_source.width):
            mail_source.putpixel((x, y), original_mail.getpixel((50 - x, y)))
    mail = b.flood_remove_background(mail_source, tolerance=40)
    mail = keep_largest_component(remove_reference_green(mail))
    save(normalize_common(mail), "Common/Icon_Mail.png")
    badge = b.flood_remove_background(main_ref.crop((750, 96, 773, 119)), tolerance=45)
    badge = keep_largest_component(remove_reference_green(badge))
    save(normalize_common(badge), "Common/Badge_Notification.png")
    search = b.flood_remove_background(equipment_ref.crop((246, 548, 287, 589)), tolerance=38)
    save(normalize_common(keep_largest_component(search)), "Common/Icon_Search.png")
    # Replace the low-resolution reference crops with high-resolution masters.
    # Unity scales these 256px sources down to the 64px UI slot; never enlarge
    # the 29-55px crops because that visibly softens their outlines.
    export_high_res_common()
    export_inventory_category_icons()
    fix_tab_state_dimensions()
    # Restore the equipment-specific composites and stat glyphs directly from
    # the approved Equipment reference. Never delete this directory again.
    b.extract_equipment()
    export_main_reference_hud(main_ref, b)
    export_remade_main_hud()

    # Main profile/currency components. These are separate empty backplates
    # matching the actual reference structure: the profile fades out on its
    # right edge, while only the currency plate is a rounded capsule.
    profile_panel = profile_backplate()
    currency_panel = currency_backplate()
    save(profile_panel, "Main/Profile_Panel.png")
    save(currency_panel, "Main/Currency_Panel.png")
    avatar = shop_ref.crop((18, 24, 88, 93)).convert("RGBA")
    avatar.putalpha(Image.new("L", avatar.size, 255))
    save(avatar, "Main/Profile_Avatar.png")
    # Preserve the actual reference pixels. Do not redraw or upscale these.
    gold = b.flood_remove_background(main_ref.crop((548, 28, 610, 86)), 38)
    gem = b.flood_remove_background(main_ref.crop((720, 26, 790, 86)), 38)
    gold = tight(keep_largest_component(gold))
    gem = tight(keep_largest_component(gem))
    save(gold, "Main/Currency_Gold.png")
    save(gem, "Main/Currency_Gem.png")
    exp_bg = Image.new("RGBA", (256, 16), (19, 29, 35, 255))
    exp_fill = Image.new("RGBA", (256, 16), (50, 174, 194, 255))
    exp_frame = Image.new("RGBA", (256, 16), (0, 0, 0, 0))
    ImageDraw.Draw(exp_frame).rounded_rectangle((0, 0, 255, 15), 7, outline=(91, 103, 101, 255), width=2)
    save(exp_bg, "Main/Profile_EXP_Background.png")
    save(exp_fill, "Main/Profile_EXP_Fill.png")
    save(exp_frame, "Main/Profile_EXP_Frame.png")

    hp_potion = b.flood_remove_background(shop_ref.crop((54, 1022, 197, 1212)), tolerance=38)
    hp_potion = tight(normalize(hp_potion, (96, 96), 10))
    save(hp_potion, "Main/Potion_HP.png")
    mp_potion = hp_potion.copy()
    pixels = mp_potion.load()
    for y in range(mp_potion.height):
        for x in range(mp_potion.width):
            r, g, blue, a = pixels[x, y]
            if a and r > g * 1.1 and r > blue * 1.1:
                pixels[x, y] = (max(15, blue), min(170, g + 35), min(255, r), a)
    save(mp_potion, "Main/Potion_MP.png")
    export_cartoon_potions()
    export_reference_style_potions()
    apply_stage_plate_alpha()

    # Reference skill artwork only; remove frames, badges and labels.
    xs = [(92, 228), (293, 431), (497, 635), (702, 840)]
    ys = [(318, 444), (555, 684), (794, 923), (1028, 1158)]
    index = 1
    for top, bottom in ys:
        for left, right in xs:
            art = skill_ref.crop((left + 22, top + 22, right - 16, bottom - 26))
            save(art.resize((128, 128), Image.Resampling.LANCZOS), f"Skill/Skill_{index:02d}.png")
            index += 1

    # Exact dungeon illustrations and rewards.
    dungeon_art = [
        ("ForgottenCrypt", (47, 363, 353, 687)),
        ("FrozenThrone", (47, 714, 353, 998)),
        ("DragonHeart", (47, 1063, 353, 1383)),
        ("InfiniteAbyss", (47, 1407, 353, 1532)),
    ]
    for name, box in dungeon_art:
        save(dungeon_ref.crop(box), f"Dungeon/Art_{name}.png")
    rewards = [
        ("Gold", (394, 622, 439, 670)), ("PurpleGem", (465, 622, 507, 670)),
        ("BlueGem", (465, 973, 507, 1021)), ("Sword", (534, 973, 577, 1021)),
        ("RedGem", (465, 1323, 507, 1371)),
    ]
    for name, box in rewards:
        art = b.flood_remove_background(dungeon_ref.crop(box), tolerance=38)
        save(tight(normalize(art, (96, 96), 12)), f"Dungeon/Reward_{name}.png")
    for name, box in (
        ("Star_Active", (385, 523, 418, 557)),
        ("Star_Inactive", (469, 523, 502, 557)),
        ("Ticket_Dungeon", (42, 202, 100, 260)),
    ):
        art = b.flood_remove_background(dungeon_ref.crop(box), tolerance=40)
        if name == "Ticket_Dungeon":
            art = keep_largest_component(art)
        save(tight(normalize(art, (72, 72), 9)), f"Dungeon/{name}.png")
    save(Image.open(OUT / "Dungeon/Star_Active.png"), "Main/Icon_Star.png")

    # Exact reference shop product art, excluding cards and text.
    shops = [
        ("FeaturedBundle", (367, 366, 701, 590)), ("DailyGift", (48, 626, 128, 711)),
        ("EquipmentChest", (44, 782, 211, 958)), ("GemBundle", (493, 785, 659, 944)),
        ("GrowthElixir", (54, 1022, 197, 1212)), ("SkillBook", (493, 1026, 653, 1215)),
        ("DungeonTicket", (51, 1279, 194, 1473)), ("ExpBooster", (493, 1277, 668, 1469)),
    ]
    for name, box in shops:
        art = b.flood_remove_background(shop_ref.crop(box), tolerance=38)
        art = remove_small_components(art, 80)
        art = restore_enclosed_alpha(art)
        save(tight(normalize(art, (256, 256), 18)), f"Shop/Product_{name}.png")

    # Rebuild the ten bottom buttons with the restored reference glyphs.
    normal_frame = Image.open(OUT / "States/Nav_Frame_Normal.png").convert("RGBA")
    selected_frame = Image.open(OUT / "States/Nav_Frame_Selected.png").convert("RGBA")
    for name in ("Equipment", "Dungeon", "Main", "Skill", "Shop"):
        glyph = Image.open(OUT / "Common" / f"Nav_{name}.png").convert("RGBA")
        glyph = upscale_to_fit(glyph, (78, 78))
        glyph_canvas = Image.new("RGBA", normal_frame.size, (0, 0, 0, 0))
        glyph_canvas.alpha_composite(
            glyph,
            ((normal_frame.width - glyph.width) // 2,
             (normal_frame.height - glyph.height) // 2),
        )
        normal = normal_frame.copy()
        normal.alpha_composite(glyph_canvas)
        save(normal, f"States/Nav_{name}_Normal.png")
        selected = selected_frame.copy()
        selected.alpha_composite(glyph_canvas)
        save(selected, f"States/Nav_{name}_Selected.png")

    groups = {
        folder.name: len(list(folder.glob("*.png")))
        for folder in OUT.iterdir() if folder.is_dir()
    }
    manifest = {
        "art_source": "exact crops from the five approved BrightReferences",
        "generated_artwork": False,
        "equipment_items": "../Equipment/Items",
        "groups": groups,
        "rules": [
            "All text and changing numbers use TextMeshPro.",
            "Equipment artwork uses the preserved 75 cartoon items.",
            "Slider layers in each set share identical dimensions.",
        ],
    }
    (OUT / "manifest.json").write_text(
        json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8"
    )

    for panel_name in ("Panel_Dark.png", "Panel_Light.png"):
        panel_path = OUT / "Frames" / panel_name
        if panel_path.exists():
            save(restore_panel_bottom(Image.open(panel_path)), f"Frames/{panel_name}")

    # Individual sprites are trimmed to their actual alpha bounds. Do not add
    # forced transparent canvas margins; sliders remain fixed-size because
    # their layers must align in Unity.
    slider_targets = {
        "Stage": (520, 72),
        "EXP": (520, 40),
        "Orb": (264, 264),
    }
    for png in OUT.rglob("*.png"):
        image = Image.open(png).convert("RGBA")
        if png.parent.name == "Sliders":
            prefix = png.stem.split("_")[0]
            target = slider_targets[prefix]
            if image.size != target:
                image = add_unity_safe_padding(image, 4)
        elif png.parent.name != "Common":
            image = tight(image)
        image.save(png, optimize=True)

    print("Reference artwork restored")


if __name__ == "__main__":
    main()
