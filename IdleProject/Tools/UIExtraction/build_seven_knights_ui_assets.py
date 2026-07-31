#!/usr/bin/env python3
from __future__ import annotations

import json
from collections import deque
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter


ROOT = Path(__file__).resolve().parents[2]
REF = ROOT / "Assets/05_Resources/UI/BrightReferences"
OUT = ROOT / "Assets/05_Resources/UI/BrightTheme"
EQUIPMENT = ROOT / "Assets/05_Resources/UI/Equipments"

W, H = 942, 1670


def ensure_dirs() -> None:
    for path in (
        OUT / "Common/Frames",
        OUT / "Common/Icons",
        OUT / "Main",
        OUT / "Equipment",
        OUT / "Dungeon",
        OUT / "Skill",
        OUT / "Shop",
        OUT / "Compatibility",
    ):
        path.mkdir(parents=True, exist_ok=True)


def load(name: str) -> Image.Image:
    image = Image.open(REF / name).convert("RGBA")
    if image.size != (W, H):
        image = image.resize((W, H), Image.Resampling.LANCZOS)
    return image


def save(image: Image.Image, relative: str) -> None:
    target = OUT / relative
    target.parent.mkdir(parents=True, exist_ok=True)
    image.save(target, optimize=True)


def crop(image: Image.Image, box: tuple[int, int, int, int], relative: str) -> Image.Image:
    result = image.crop(box)
    save(result, relative)
    return result


def masked_crop(
    image: Image.Image,
    box: tuple[int, int, int, int],
    relative: str,
    shape: str = "rounded",
    radius: int = 14,
    inset: int = 1,
) -> Image.Image:
    result = image.crop(box).convert("RGBA")
    mask = Image.new("L", result.size, 0)
    draw = ImageDraw.Draw(mask)
    bounds = (inset, inset, result.width - 1 - inset, result.height - 1 - inset)
    if shape == "ellipse":
        draw.ellipse(bounds, fill=255)
    else:
        draw.rounded_rectangle(bounds, radius=radius, fill=255)
    result.putalpha(mask.filter(ImageFilter.GaussianBlur(0.35)))
    save(result, relative)
    return result


def rounded_mask(size: tuple[int, int], radius: int) -> Image.Image:
    mask = Image.new("L", size, 0)
    ImageDraw.Draw(mask).rounded_rectangle(
        (1, 1, size[0] - 2, size[1] - 2), radius=radius, fill=255
    )
    return mask


def make_nine_slice(
    name: str,
    face: tuple[int, int, int, int],
    outline: tuple[int, int, int, int],
    inner: tuple[int, int, int, int],
    radius: int = 12,
    selected: bool = False,
) -> dict:
    size = 64
    image = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    draw.rounded_rectangle((1, 1, 62, 62), radius=radius, fill=outline)
    draw.rounded_rectangle((3, 3, 60, 60), radius=max(1, radius - 2), fill=inner)
    draw.rounded_rectangle((5, 5, 58, 58), radius=max(1, radius - 4), fill=face)
    draw.line((10, 6, 54, 6), fill=(255, 255, 255, 48), width=1)
    if selected:
        draw.line((10, 58, 54, 58), fill=(238, 201, 132, 220), width=2)
    save(image, f"Common/Frames/{name}.png")
    return {"file": f"Common/Frames/{name}.png", "border": [12, 12, 12, 12]}


def make_slot(name: str, face: tuple[int, int, int, int], line: tuple[int, int, int, int]) -> dict:
    image = Image.new("RGBA", (96, 96), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    draw.rounded_rectangle((2, 2, 93, 93), radius=10, fill=(20, 31, 39, 255))
    draw.rounded_rectangle((4, 4, 91, 91), radius=8, outline=line, width=2)
    draw.rounded_rectangle((7, 7, 88, 88), radius=6, fill=face)
    draw.line((13, 8, 83, 8), fill=(255, 255, 255, 35), width=1)
    save(image, f"Common/Frames/{name}.png")
    return {"file": f"Common/Frames/{name}.png", "border": [12, 12, 12, 12]}


def flood_remove_background(image: Image.Image, tolerance: int = 34) -> Image.Image:
    """Remove colors connected to the edge; preserves enclosed icon details."""
    src = image.convert("RGBA")
    px = src.load()
    width, height = src.size
    samples = []
    for x in range(0, width, max(1, width // 20)):
        samples.extend((px[x, 0][:3], px[x, height - 1][:3]))
    for y in range(0, height, max(1, height // 20)):
        samples.extend((px[0, y][:3], px[width - 1, y][:3]))
    bg = tuple(sorted(channel)[len(channel) // 2] for channel in zip(*samples))

    alpha = Image.new("L", src.size, 255)
    apx = alpha.load()
    seen = bytearray(width * height)
    queue: deque[tuple[int, int]] = deque()
    for x in range(width):
        queue.append((x, 0))
        queue.append((x, height - 1))
    for y in range(height):
        queue.append((0, y))
        queue.append((width - 1, y))

    def close(rgb: tuple[int, int, int]) -> bool:
        return max(abs(rgb[i] - bg[i]) for i in range(3)) <= tolerance

    while queue:
        x, y = queue.popleft()
        idx = y * width + x
        if seen[idx]:
            continue
        seen[idx] = 1
        if not close(px[x, y][:3]):
            continue
        apx[x, y] = 0
        if x:
            queue.append((x - 1, y))
        if x + 1 < width:
            queue.append((x + 1, y))
        if y:
            queue.append((x, y - 1))
        if y + 1 < height:
            queue.append((x, y + 1))

    alpha = alpha.filter(ImageFilter.GaussianBlur(0.45))
    src.putalpha(alpha)
    bbox = src.getbbox()
    if bbox:
        src = src.crop(bbox)
    canvas_size = max(src.width, src.height) + 16
    canvas = Image.new("RGBA", (canvas_size, canvas_size), (0, 0, 0, 0))
    canvas.alpha_composite(src, ((canvas_size - src.width) // 2, (canvas_size - src.height) // 2))
    return canvas


def extract_icon(
    source: Image.Image,
    box: tuple[int, int, int, int],
    relative: str,
    remove_bg: bool = True,
) -> None:
    icon = source.crop(box)
    if remove_bg:
        icon = flood_remove_background(icon)
    icon.thumbnail((256, 256), Image.Resampling.LANCZOS)
    save(icon, relative)


def extract_main() -> None:
    main = load("Main_SevenKnights_Rebuilt.png")
    masked_crop(main, (17, 27, 386, 126), "Main/PlayerStrip.png", radius=16)
    masked_crop(main, (557, 31, 920, 92), "Main/CurrencyStrip.png", radius=25)
    masked_crop(main, (349, 150, 590, 207), "Main/StageChip.png", radius=25)
    crop(main, (305, 207, 638, 287), "Main/StageProgress.png")
    masked_crop(main, (714, 1179, 925, 1307), "Main/DPSChip.png", radius=12)
    masked_crop(main, (25, 1317, 171, 1463), "Main/Orb_HP.png", shape="ellipse", inset=2)
    masked_crop(main, (771, 1317, 917, 1463), "Main/Orb_MP.png", shape="ellipse", inset=2)
    masked_crop(main, (363, 1199, 462, 1298), "Main/Potion_HP_Cooldown.png", shape="ellipse", inset=3)
    masked_crop(main, (477, 1199, 576, 1298), "Main/Potion_MP_Cooldown.png", shape="ellipse", inset=3)

    skill_boxes = [
        (191, 1322, 296, 1444),
        (304, 1322, 409, 1444),
        (417, 1322, 522, 1444),
        (530, 1322, 635, 1444),
        (643, 1322, 748, 1444),
    ]
    for index, box in enumerate(skill_boxes, 1):
        framed = masked_crop(main, box, f"Main/Skill_{index:02d}_Framed.png", radius=9)
        inner = framed.crop((8, 8, framed.width - 8, framed.height - 19))
        inner.putalpha(rounded_mask(inner.size, 7))
        save(inner, f"Main/Skill_{index:02d}_Icon.png")

    nav_boxes = [
        ("Equipment", (40, 1483, 154, 1645)),
        ("Dungeon", (221, 1483, 337, 1645)),
        ("Main", (405, 1474, 535, 1653)),
        ("Skill", (599, 1483, 713, 1645)),
        ("Shop", (789, 1483, 904, 1645)),
    ]
    for name, box in nav_boxes:
        extract_icon(main, box, f"Common/Icons/Nav_{name}.png", remove_bg=True)

    utility = [
        ("Mail", (726, 94, 780, 152)),
        ("Codex", (797, 94, 851, 152)),
        ("Settings", (868, 94, 924, 152)),
    ]
    for name, box in utility:
        extract_icon(main, box, f"Common/Icons/Icon_{name}.png", remove_bg=True)


def extract_equipment() -> None:
    image = load("Equipment_SevenKnights_Rebuilt.png")
    masked_crop(image, (15, 183, 309, 623), "Equipment/StatsPanel_Composite.png", radius=8)
    masked_crop(image, (323, 184, 924, 623), "Equipment/PaperdollPanel_Composite.png", radius=10)
    masked_crop(image, (15, 1213, 927, 1450), "Equipment/DetailPanel_Composite.png", radius=10)

    stat_boxes = [
        ("CombatPower", (33, 208, 69, 244)),
        ("Attack", (33, 260, 69, 296)),
        ("Defense", (33, 305, 69, 341)),
        ("Health", (33, 351, 69, 387)),
        ("CriticalChance", (33, 397, 69, 433)),
        ("CriticalDamage", (33, 442, 69, 478)),
        ("AttackSpeed", (33, 489, 69, 525)),
    ]
    for name, box in stat_boxes:
        extract_icon(image, box, f"Equipment/Icon_Stat_{name}.png", remove_bg=True)

    extract_icon(image, (246, 548, 287, 589), "Equipment/Icon_Search.png", remove_bg=True)

    # The reference's realistic inventory artwork is intentionally not exported.
    # These samples are the actual game items centered on the canonical new slot.
    item_names = sorted(EQUIPMENT.glob("Item_*.png"))
    for item_path in item_names:
        item = Image.open(item_path).convert("RGBA")
        bbox = item.getchannel("A").getbbox()
        if bbox:
            item = item.crop(bbox)
        canvas = Image.new("RGBA", (128, 128), (0, 0, 0, 0))
        item.thumbnail((108, 108), Image.Resampling.LANCZOS)
        canvas.alpha_composite(item, ((128 - item.width) // 2, (128 - item.height) // 2))
        save(canvas, f"Equipment/Items/{item_path.stem}.png")


def extract_dungeon() -> None:
    image = load("Dungeon_SevenKnights_Rebuilt.png")
    cards = [
        ("ForgottenCrypt", (42, 358, 358, 692)),
        ("FrozenThrone", (42, 709, 358, 1003)),
        ("DragonHeart", (42, 1058, 358, 1388)),
        ("InfiniteAbyss", (42, 1402, 358, 1537)),
    ]
    for name, box in cards:
        crop(image, box, f"Dungeon/Thumbnail_{name}.png")

    reward_boxes = [
        ("Gold", (386, 613, 447, 679)),
        ("PurpleGem", (455, 613, 516, 679)),
        ("BlueGem", (455, 965, 516, 1030)),
        ("Sword", (525, 965, 585, 1030)),
        ("RedGem", (455, 1315, 516, 1380)),
    ]
    for name, box in reward_boxes:
        extract_icon(image, box, f"Dungeon/Reward_{name}.png", remove_bg=True)


def extract_skill() -> None:
    image = load("Skill_SevenKnights_Rebuilt.png")
    xs = [(92, 228), (293, 431), (497, 635), (702, 840)]
    ys = [(318, 444), (555, 684), (794, 923), (1028, 1158)]
    index = 1
    for row, (top, bottom) in enumerate(ys, 1):
        for col, (left, right) in enumerate(xs, 1):
            crop(
                image,
                (left, top, right, bottom),
                f"Skill/Skill_{index:02d}_R{row}C{col}_Framed.png",
            )
            framed = image.crop((left, top, right, bottom)).convert("RGBA")
            inner = framed.crop((10, 8, framed.width - 10, framed.height - 20))
            inner.putalpha(rounded_mask(inner.size, 8))
            save(inner, f"Skill/Skill_{index:02d}_R{row}C{col}_Icon.png")
            index += 1


def extract_shop() -> None:
    image = load("Shop_SevenKnights_Rebuilt.png")
    crops = [
        ("FeaturedBundle", (380, 379, 689, 589), False),
        ("DailyGift", (38, 610, 205, 725), False),
        ("EquipmentChest", (37, 748, 220, 987), True),
        ("GemBundle", (477, 749, 676, 987), True),
        ("GrowthElixir", (38, 1002, 220, 1243), True),
        ("SkillBook", (475, 1002, 681, 1243), True),
        ("DungeonTicket", (39, 1255, 218, 1499), True),
        ("ExpBooster", (475, 1255, 681, 1499), True),
    ]
    for name, box, remove in crops:
        extract_icon(image, box, f"Shop/Product_{name}.png", remove_bg=remove)


def build_compatibility_sheet() -> dict:
    names = [
        "Item_03_IronAxe.png",
        "Item_04_PlateHelmet.png",
        "Item_05_PlateArmor.png",
        "Item_10_RingOfStrength.png",
        "Item_13_NecklaceOfRage.png",
        "Item_16_LegendaryInfernoAxe.png",
        "Item_18_LegendaryLionguardHelmet.png",
        "Item_20_LegendaryCrystalFortressArmor.png",
        "Item_30_LegendaryDragonEyeRing.png",
        "Item_32_LegendaryPhoenixNecklace.png",
    ]
    slot = Image.open(OUT / "Common/Frames/Slot_Item.png").convert("RGBA")
    sheet = Image.new("RGBA", (5 * 150, 2 * 178), (22, 34, 43, 255))
    draw = ImageDraw.Draw(sheet)
    brightness = []
    saturation = []
    for index, name in enumerate(names):
        item = Image.open(EQUIPMENT / name).convert("RGBA")
        item.thumbnail((106, 106), Image.Resampling.LANCZOS)
        frame = slot.resize((126, 126), Image.Resampling.LANCZOS)
        x = (index % 5) * 150 + 12
        y = (index // 5) * 178 + 10
        sheet.alpha_composite(frame, (x, y))
        sheet.alpha_composite(item, (x + (126 - item.width) // 2, y + (126 - item.height) // 2))
        draw.text((x, y + 132), name.replace("Item_", "").replace(".png", "")[:19], fill=(230, 225, 211, 255))
        opaque = [p for p in item.getdata() if p[3] > 32]
        if opaque:
            brightness.append(sum((p[0] + p[1] + p[2]) / 3 for p in opaque) / len(opaque))
            saturation.append(
                sum(max(p[:3]) - min(p[:3]) for p in opaque) / len(opaque)
            )
    save(sheet, "Compatibility/ExistingEquipment_OnNewSlots.png")
    return {
        "sample_count": len(names),
        "mean_brightness": round(sum(brightness) / len(brightness), 1),
        "mean_saturation_range": round(sum(saturation) / len(saturation), 1),
        "assessment": (
            "Compatible: existing equipment uses transparent, centered, cartoon-rendered "
            "silhouettes. Neutral slate slots reduce the realism mismatch of generated "
            "reference items. Reuse existing Item_*.png assets rather than extracting "
            "the generated equipment artwork."
        ),
    }


def main() -> None:
    ensure_dirs()
    manifest = {
        "source_reference_size": [W, H],
        "pixels_per_unit": 100,
        "nine_slice": [],
        "notes": [
            "Common frames are canonical redraws, not crops containing baked text.",
            "Use Image.Type=Sliced for files listed under nine_slice.",
            "Generated equipment artwork is intentionally excluded; reuse Equipments/Item_*.png.",
        ],
    }
    manifest["nine_slice"].extend(
        [
            make_nine_slice("Panel_Slate", (31, 48, 59, 242), (15, 26, 33, 255), (101, 116, 123, 255)),
            make_nine_slice("Panel_Slate_Translucent", (31, 48, 59, 218), (15, 26, 33, 240), (101, 116, 123, 220)),
            make_nine_slice("Panel_Light", (216, 218, 216, 255), (34, 46, 55, 255), (141, 148, 151, 255)),
            make_nine_slice("Chip_Dark", (37, 51, 61, 255), (17, 27, 34, 255), (118, 127, 132, 255), radius=18),
            make_nine_slice("Button_Coral", (190, 75, 67, 255), (86, 46, 45, 255), (230, 132, 119, 255), radius=10),
            make_nine_slice("Tab_Idle", (35, 48, 58, 255), (19, 30, 38, 255), (104, 113, 118, 255), radius=8),
            make_nine_slice("Tab_Selected", (178, 74, 69, 255), (73, 42, 43, 255), (232, 132, 119, 255), radius=8, selected=True),
        ]
    )
    manifest["nine_slice"].append(
        make_slot("Slot_Item", (55, 66, 70, 255), (145, 151, 150, 255))
    )
    manifest["nine_slice"].append(
        make_slot("Slot_Skill", (29, 40, 50, 255), (137, 143, 145, 255))
    )
    extract_main()
    extract_equipment()
    extract_dungeon()
    extract_skill()
    extract_shop()
    manifest["equipment_compatibility"] = build_compatibility_sheet()

    with (OUT / "sprite_manifest.json").open("w", encoding="utf-8") as handle:
        json.dump(manifest, handle, ensure_ascii=False, indent=2)


if __name__ == "__main__":
    main()
