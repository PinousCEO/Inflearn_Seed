#!/usr/bin/env python3
import json
from pathlib import Path
from PIL import Image, ImageDraw, ImageEnhance

ROOT = Path(__file__).resolve().parents[2]
SRC = ROOT / "tmp/imagegen/rebuilt-ui"
OUT = ROOT / "Assets/05_Resources/UI/BrightTheme/Recreated"

SHEETS = {
    "common": (4, 3, "Common", [
        "Icon_Mail", "Icon_Codex", "Icon_Settings", "Badge_Notification",
        "Nav_Equipment", "Nav_Dungeon", "Nav_Main", "Nav_Skill",
        "Nav_Shop", "Icon_Gift", "Icon_Log", "Icon_Search",
    ]),
    "main": (4, 3, "Main", [
        "Orb_Frame", "Orb_HP", "Orb_MP", "Potion_HP",
        "Potion_MP", "Currency_Gold", "Currency_Gem", "Stage_Frame",
        "Stage_Fill", "Hud_Chip", "Quick_Frame", "Icon_Star",
    ]),
    "skill": (4, 4, "Skill", [f"Skill_{i:02d}" for i in range(1, 17)]),
    "dungeon": (4, 3, "Dungeon", [
        "Art_ForgottenCrypt", "Art_FrozenThrone", "Art_DragonHeart", "Art_InfiniteAbyss",
        "Reward_Gold", "Reward_PurpleGem", "Reward_BlueGem", "Reward_RedGem",
        "Reward_Sword", "Star_Active", "Star_Inactive", "Ticket_Dungeon",
    ]),
    "shop": (4, 2, "Shop", [
        "Product_FeaturedBundle", "Product_DailyGift", "Product_EquipmentChest", "Product_GemBundle",
        "Product_GrowthElixir", "Product_SkillBook", "Product_DungeonTicket", "Product_ExpBooster",
    ]),
    "equipment": (5, 4, "Equipment", [
        "Item_Sword", "Item_Dagger", "Item_Staff", "Item_Bow", "Item_Spellbook",
        "Item_RubyNecklace", "Item_BlueNecklace", "Item_RubyRing", "Item_SapphireRing", "Item_TripleGemRing",
        "Item_ChestArmor", "Item_Helmet", "Item_BlueGlove", "Item_LeatherGlove", "Item_FurCloak",
        "Item_LeatherCloak", "Item_GreenRing", "Item_EmeraldRing", "Item_Belt", "Item_Boots",
    ]),
    "frames": (4, 3, "Frames", [
        "Panel_Dark", "Panel_Light", "Button_Primary", "Button_Secondary",
        "Tab_Selected", "Tab_Idle", "Slot_Item", "Slot_Skill",
        "Slot_Reward", "Frame_NavigationHex", "Row_Stat", "Panel_Tooltip",
    ]),
    "states": (4, 2, "States", [
        "Rarity_Common", "Rarity_Rare", "Rarity_Epic", "Rarity_Legendary",
        "Rarity_Unique", "Nav_Frame_Normal", "Nav_Frame_Selected", "Slot_Item_Selected",
    ]),
}


def largest_component(cell: Image.Image) -> Image.Image:
    cell = cell.convert("RGBA")
    alpha = cell.getchannel("A")
    w, h = cell.size
    seen = bytearray(w * h)
    groups = []
    for sy in range(h):
        for sx in range(w):
            start = sy * w + sx
            if seen[start] or alpha.getpixel((sx, sy)) < 20:
                continue
            seen[start] = 1
            stack = [(sx, sy)]
            group = []
            while stack:
                x, y = stack.pop()
                group.append((x, y))
                for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
                    if 0 <= nx < w and 0 <= ny < h:
                        idx = ny * w + nx
                        if not seen[idx] and alpha.getpixel((nx, ny)) >= 20:
                            seen[idx] = 1
                            stack.append((nx, ny))
            groups.append(group)
    if not groups:
        return cell
    keep = set(max(groups, key=len))
    pixels = cell.load()
    for y in range(h):
        for x in range(w):
            if (x, y) not in keep:
                pixels[x, y] = (*pixels[x, y][:3], 0)
    return cell


def isolate(cell: Image.Image, canvas_size: int) -> Image.Image:
    cell = largest_component(cell)
    cell = cell.convert("RGBA")
    alpha = cell.getchannel("A")
    bbox = alpha.getbbox()
    if not bbox:
        return Image.new("RGBA", (canvas_size, canvas_size), (0, 0, 0, 0))
    art = cell.crop(bbox)
    margin = max(12, canvas_size // 12)
    art.thumbnail((canvas_size - margin * 2, canvas_size - margin * 2), Image.Resampling.LANCZOS)
    canvas = Image.new("RGBA", (canvas_size, canvas_size), (0, 0, 0, 0))
    canvas.alpha_composite(art, ((canvas_size - art.width) // 2, (canvas_size - art.height) // 2))
    return canvas


def strip_orb_frame(image: Image.Image) -> Image.Image:
    image = image.convert("RGBA")
    alpha = image.getchannel("A")
    bbox = alpha.getbbox()
    if not bbox:
        return image
    cx = (bbox[0] + bbox[2]) / 2
    cy = (bbox[1] + bbox[3]) / 2
    radius = min(bbox[2] - bbox[0], bbox[3] - bbox[1]) * 0.37
    pixels = image.load()
    for y in range(image.height):
        for x in range(image.width):
            if ((x + 0.5 - cx) ** 2 + (y + 0.5 - cy) ** 2) ** 0.5 > radius:
                pixels[x, y] = (*pixels[x, y][:3], 0)
    return image


def trim_transparent_canvas(image: Image.Image) -> Image.Image:
    """Crop only fully transparent outer pixels; preserve every visible pixel."""
    image = image.convert("RGBA")
    bbox = image.getchannel("A").getbbox()
    return image.crop(bbox) if bbox else image


def vertical_gradient(size, top, bottom, mask):
    image = Image.new("RGBA", size, (0, 0, 0, 0))
    pixels = image.load()
    for y in range(size[1]):
        t = y / max(1, size[1] - 1)
        color = tuple(int(top[i] * (1 - t) + bottom[i] * t) for i in range(4))
        for x in range(size[0]):
            pixels[x, y] = color
    image.putalpha(mask)
    return image


def fill_rarity_frame(frame, top, bottom):
    frame = frame.convert("RGBA")
    bbox = frame.getchannel("A").getbbox()
    mask = Image.new("L", frame.size, 0)
    inset = 10
    ImageDraw.Draw(mask).rounded_rectangle(
        (bbox[0] + inset, bbox[1] + inset, bbox[2] - inset, bbox[3] - inset),
        radius=14, fill=255,
    )
    result = vertical_gradient(frame.size, top, bottom, mask)
    result.alpha_composite(frame)
    return result


def fill_hex_frame(frame, top, bottom):
    frame = frame.convert("RGBA")
    bbox = frame.getchannel("A").getbbox()
    inset = 9
    left, top_y, right, bottom_y = (
        bbox[0] + inset, bbox[1] + inset, bbox[2] - inset, bbox[3] - inset
    )
    cx = (left + right) / 2
    points = [
        (cx, top_y), (right, top_y + (bottom_y - top_y) * .25),
        (right, top_y + (bottom_y - top_y) * .75), (cx, bottom_y),
        (left, top_y + (bottom_y - top_y) * .75),
        (left, top_y + (bottom_y - top_y) * .25),
    ]
    mask = Image.new("L", frame.size, 0)
    ImageDraw.Draw(mask).polygon(points, fill=255)
    result = vertical_gradient(frame.size, top, bottom, mask)
    result.alpha_composite(frame)
    return result


def clipped_bar_mask(size, inset=0):
    w, h = size
    cut = max(4, h // 4)
    return [
        (inset + cut, inset), (w - inset - cut - 1, inset),
        (w - inset - 1, inset + cut), (w - inset - 1, h - inset - cut - 1),
        (w - inset - cut - 1, h - inset - 1), (inset + cut, h - inset - 1),
        (inset, h - inset - cut - 1), (inset, inset + cut),
    ]


def make_slider_set(prefix, size, fill_top, fill_bottom):
    target = OUT / "Sliders"
    target.mkdir(parents=True, exist_ok=True)
    background = Image.new("RGBA", size, (0, 0, 0, 0))
    bg_draw = ImageDraw.Draw(background)
    bg_draw.polygon(clipped_bar_mask(size), fill=(18, 27, 34, 255))
    background.save(target / f"{prefix}_Background.png", optimize=True)

    fill_mask = Image.new("L", size, 0)
    ImageDraw.Draw(fill_mask).polygon(clipped_bar_mask(size, 5), fill=255)
    fill = vertical_gradient(size, fill_top, fill_bottom, fill_mask)
    fill.save(target / f"{prefix}_Fill.png", optimize=True)

    frame = Image.new("RGBA", size, (0, 0, 0, 0))
    frame_draw = ImageDraw.Draw(frame)
    frame_draw.line(
        clipped_bar_mask(size) + [clipped_bar_mask(size)[0]],
        fill=(181, 157, 104, 255), width=3, joint="curve",
    )
    frame_draw.line(
        clipped_bar_mask(size, 4) + [clipped_bar_mask(size, 4)[0]],
        fill=(65, 74, 74, 255), width=2, joint="curve",
    )
    frame.save(target / f"{prefix}_Frame.png", optimize=True)


def make_orb_slider_set():
    target = OUT / "Sliders"
    target.mkdir(parents=True, exist_ok=True)
    size = (256, 256)
    background = Image.new("RGBA", size, (0, 0, 0, 0))
    ImageDraw.Draw(background).ellipse((1, 1, 254, 254), fill=(18, 26, 32, 255))
    background.save(target / "Orb_Background.png", optimize=True)

    for name, top, bottom in (
        ("HP", (190, 61, 53, 255), (78, 13, 20, 255)),
        ("MP", (53, 115, 202, 255), (12, 38, 86, 255)),
    ):
        mask = Image.new("L", size, 0)
        ImageDraw.Draw(mask).ellipse((8, 8, 247, 247), fill=255)
        vertical_gradient(size, top, bottom, mask).save(
            target / f"Orb_{name}_Fill.png", optimize=True
        )

    frame = Image.new("RGBA", size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(frame)
    draw.ellipse((1, 1, 254, 254), outline=(174, 158, 119, 255), width=8)
    draw.ellipse((11, 11, 244, 244), outline=(54, 64, 65, 255), width=6)
    frame.save(target / "Orb_Frame.png", optimize=True)


def main() -> None:
    counts = {}
    for sheet_name, (cols, rows, folder, names) in SHEETS.items():
        source = Image.open(SRC / f"{sheet_name}-alpha.png").convert("RGBA")
        target = OUT / folder
        target.mkdir(parents=True, exist_ok=True)
        width, height = source.size
        canvas_size = 384 if folder == "Dungeon" and names[0].startswith("Art_") else 256
        for index, name in enumerate(names):
            col, row = index % cols, index // cols
            left = round(col * width / cols)
            top = round(row * height / rows)
            right = round((col + 1) * width / cols)
            bottom = round((row + 1) * height / rows)
            size = 384 if name.startswith("Art_") else canvas_size
            asset = isolate(source.crop((left, top, right, bottom)), size)
            if name in ("Orb_HP", "Orb_MP"):
                asset = strip_orb_frame(asset)
            asset.save(target / f"{name}.png", optimize=True)
        counts[folder] = len(names)
    for source_name, output_name, size in (
        ("row-stat-alpha.png", "Row_Stat.png", 512),
        ("tooltip-alpha.png", "Panel_Tooltip.png", 512),
    ):
        source = Image.open(SRC / source_name).convert("RGBA")
        isolate(source, size).save(OUT / "Frames" / output_name, optimize=True)
    underline = Image.new("RGBA", (256, 64), (0, 0, 0, 0))
    from PIL import ImageDraw
    draw = ImageDraw.Draw(underline)
    draw.rounded_rectangle((40, 27, 215, 35), radius=4, fill=(238, 203, 126, 255))
    underline.save(OUT / "States" / "Nav_SelectedUnderline.png", optimize=True)
    counts["States"] += 1

    rarity_colors = {
        "Common": ((62, 70, 73, 255), (34, 42, 47, 255)),
        "Rare": ((42, 76, 105, 255), (24, 45, 69, 255)),
        "Epic": ((79, 59, 103, 255), (43, 34, 63, 255)),
        "Legendary": ((112, 83, 35, 255), (57, 42, 25, 255)),
        "Unique": ((117, 56, 50, 255), (65, 34, 37, 255)),
    }
    for rarity, colors in rarity_colors.items():
        path = OUT / "States" / f"Rarity_{rarity}.png"
        filled = fill_rarity_frame(Image.open(path), colors[0], colors[1])
        filled.save(path, optimize=True)

    normal_frame_path = OUT / "States" / "Nav_Frame_Normal.png"
    selected_frame_path = OUT / "States" / "Nav_Frame_Selected.png"
    normal_frame = fill_hex_frame(
        Image.open(normal_frame_path), (38, 52, 61, 255), (20, 31, 39, 255)
    )
    normal_frame.save(normal_frame_path, optimize=True)
    selected_frame = Image.open(selected_frame_path).convert("RGBA")
    nav_names = ["Equipment", "Dungeon", "Main", "Skill", "Shop"]
    for name in nav_names:
        glyph = Image.open(OUT / "Common" / f"Nav_{name}.png").convert("RGBA")
        glyph = glyph.resize((180, 180), Image.Resampling.LANCZOS)
        glyph_canvas = Image.new("RGBA", normal_frame.size, (0, 0, 0, 0))
        glyph_canvas.alpha_composite(
            glyph,
            ((glyph_canvas.width - glyph.width) // 2,
             (glyph_canvas.height - glyph.height) // 2),
        )
        normal = normal_frame.copy()
        normal.alpha_composite(glyph_canvas)
        normal.save(OUT / "States" / f"Nav_{name}_Normal.png", optimize=True)
        selected = selected_frame.copy()
        bright = ImageEnhance.Brightness(glyph_canvas).enhance(1.25)
        selected.alpha_composite(bright)
        selected.save(OUT / "States" / f"Nav_{name}_Selected.png", optimize=True)
    counts["States"] += 10

    make_slider_set(
        "Stage", (512, 64),
        (244, 218, 132, 255), (176, 127, 48, 255),
    )
    make_slider_set(
        "EXP", (512, 32),
        (86, 218, 226, 255), (32, 142, 164, 255),
    )
    make_orb_slider_set()
    counts["Sliders"] = 10

    # Superseded composites: use only the aligned sets under Sliders.
    for old_name in (
        "Stage_Frame.png", "Stage_Fill.png",
        "Orb_Frame.png", "Orb_HP.png", "Orb_MP.png",
    ):
        old_path = OUT / "Main" / old_name
        if old_path.exists():
            old_path.unlink()
    counts["Main"] -= 5

    # Final Unity delivery uses tight canvases. This removes the generous
    # generation/slicing gutters without deleting any non-transparent pixel.
    for png in OUT.rglob("*.png"):
        if png.parent.name == "Sliders":
            continue
        image = trim_transparent_canvas(Image.open(png))
        image.save(png, optimize=True)
    manifest = {
        "status": "recreated_from_scratch",
        "source_references": ["Main", "Equipment", "Dungeon", "Skill", "Shop"],
        "sprite_count": sum(counts.values()),
        "rules": [
            "Every PNG contains one complete visual element.",
            "No rasterized labels, values, prices, cooldowns, or notification state.",
            "Use TextMeshPro for all text and numbers.",
            "Files in Frames use Unity Image Type Sliced.",
        ],
        "groups": counts,
    }
    (OUT / "manifest.json").write_text(
        json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8"
    )
    print(counts, "total", sum(counts.values()))


if __name__ == "__main__":
    main()
