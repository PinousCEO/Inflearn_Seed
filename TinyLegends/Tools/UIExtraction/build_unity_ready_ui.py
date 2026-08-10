#!/usr/bin/env python3
"""Build the Unity-consumable UI pack from five flattened screen references.

Only isolated art or deterministic, text-free controls are emitted. Screen
crops, baked labels, counters, notification states, and composite cards are
never copied into UnityReady.
"""
from __future__ import annotations

import importlib.util
import json
from pathlib import Path

from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[2]
SOURCE_SCRIPT = ROOT / "Tools/UIExtraction/build_seven_knights_ui_assets.py"
OUT = ROOT / "Assets/05_Resources/UI/BrightTheme/UnityReady"


def load_builder():
    spec = importlib.util.spec_from_file_location("bright_builder", SOURCE_SCRIPT)
    module = importlib.util.module_from_spec(spec)
    assert spec.loader
    spec.loader.exec_module(module)
    return module


def save(image: Image.Image, relative: str) -> None:
    path = OUT / relative
    path.parent.mkdir(parents=True, exist_ok=True)
    image.save(path, optimize=True)


def normalize(image: Image.Image, size: tuple[int, int], margin: int = 12) -> Image.Image:
    image = image.convert("RGBA")
    box = image.getchannel("A").getbbox()
    if box:
        image = image.crop(box)
    image.thumbnail((size[0] - margin * 2, size[1] - margin * 2), Image.Resampling.LANCZOS)
    canvas = Image.new("RGBA", size, (0, 0, 0, 0))
    canvas.alpha_composite(image, ((size[0] - image.width) // 2, (size[1] - image.height) // 2))
    return canvas


def copy_normalized(source: Path, relative: str, size=(128, 128), margin=12) -> None:
    save(normalize(Image.open(source), size, margin), relative)


def keep_largest_alpha_component(image: Image.Image) -> Image.Image:
    image = image.convert("RGBA")
    alpha = image.getchannel("A")
    width, height = image.size
    seen = bytearray(width * height)
    groups: list[list[tuple[int, int]]] = []
    for sy in range(height):
        for sx in range(width):
            idx = sy * width + sx
            if seen[idx] or alpha.getpixel((sx, sy)) < 24:
                continue
            stack = [(sx, sy)]
            seen[idx] = 1
            group = []
            while stack:
                x, y = stack.pop()
                group.append((x, y))
                for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
                    if 0 <= nx < width and 0 <= ny < height:
                        ni = ny * width + nx
                        if not seen[ni] and alpha.getpixel((nx, ny)) >= 24:
                            seen[ni] = 1
                            stack.append((nx, ny))
            groups.append(group)
    if not groups:
        return image
    keep = set(max(groups, key=len))
    pixels = image.load()
    for y in range(height):
        for x in range(width):
            if (x, y) not in keep:
                pixels[x, y] = (*pixels[x, y][:3], 0)
    return image


def rounded_art(source: Image.Image, box: tuple[int, int, int, int], relative: str) -> None:
    art = source.crop(box).convert("RGBA").resize((320, 320), Image.Resampling.LANCZOS)
    mask = Image.new("L", art.size, 0)
    ImageDraw.Draw(mask).rounded_rectangle((4, 4, 315, 315), radius=18, fill=255)
    art.putalpha(mask)
    save(normalize(art, (352, 352), 16), relative)


def main() -> None:
    b = load_builder()
    b.ensure_dirs()
    main_ref = b.load("Main_SevenKnights_Rebuilt.png")
    b.extract_runtime_common(main_ref)
    b.extract_runtime_main(main_ref)
    b.extract_skill()
    b.extract_dungeon()
    b.extract_shop()

    runtime = b.OUT / "Runtime"
    for source in sorted(runtime.rglob("*.png")):
        relative = source.relative_to(runtime).as_posix()
        image = Image.open(source)
        if source.name.startswith("Nav_Glyph_"):
            image = keep_largest_alpha_component(image)
        save(normalize(image, image.size, 4), relative)

    # Canonical text-free 9-slice controls. Preserve their exact dimensions and
    # transparent gutters so the Unity importer can apply the declared borders.
    for source in sorted((b.OUT / "Common/Frames").glob("*.png")):
        save(Image.open(source).convert("RGBA"), f"Common/Frames/{source.name}")

    # Override Main cutouts with tight art-only bounds; the broader source
    # controls contain circular slots and must never reach UnityReady.
    main_cutouts = [
        ("Potion_HP", (402, 1222, 427, 1262)),
        ("Potion_MP", (516, 1222, 541, 1262)),
        ("Quick_Gift", (38, 255, 67, 287)),
        ("Quick_Log", (39, 333, 68, 365)),
    ]
    for name, box in main_cutouts:
        art = b.flood_remove_background(main_ref.crop(box), tolerance=40)
        save(normalize(art, (64, 64), 12), f"Main/{name}.png")

    # Skill art is frame-free and normalized; selection is a separate overlay.
    for source in sorted((b.OUT / "Skill").glob("Skill_*_Icon.png")):
        copy_normalized(source, f"Skill/{source.name}", (128, 128), 16)
    copy_normalized(
        b.OUT / "Skill/Overlay_SelectedCheck.png",
        "Common/Badge_SelectedCheck.png",
        (48, 48),
        6,
    )

    # Existing equipment artwork is already independent of slots, stars and text.
    for source in sorted((b.OUT / "Equipment/Items").glob("Item_*.png")):
        copy_normalized(source, f"Equipment/Items/{source.name}", (128, 128), 12)
    for source in sorted((b.OUT / "Equipment").glob("Icon_Stat_*.png")):
        copy_normalized(source, f"Equipment/Stats/{source.name}", (64, 64), 10)

    # Dungeon card illustrations only. Frames, text, rewards and buttons are built
    # separately in Unity. Insets remove the baked card outline.
    dungeon = b.load("Dungeon_SevenKnights_Rebuilt.png")
    dungeon_art = [
        ("ForgottenCrypt", (47, 363, 353, 687)),
        ("FrozenThrone", (47, 714, 353, 998)),
        ("DragonHeart", (47, 1063, 353, 1383)),
        ("InfiniteAbyss", (47, 1407, 353, 1532)),
    ]
    for name, box in dungeon_art:
        rounded_art(dungeon, box, f"Dungeon/Art_{name}.png")
    reward_boxes = [
        ("Gold", (394, 622, 439, 670)),
        ("PurpleGem", (465, 622, 507, 670)),
        ("BlueGem", (465, 973, 507, 1021)),
        ("Sword", (534, 973, 577, 1021)),
        ("RedGem", (465, 1323, 507, 1371)),
    ]
    for name, box in reward_boxes:
        art = b.flood_remove_background(dungeon.crop(box), tolerance=38)
        save(normalize(art, (96, 96), 16), f"Dungeon/Rewards/Reward_{name}.png")

    # Product art only. Product cards, labels, prices and buttons remain Unity UI.
    shop = b.load("Shop_SevenKnights_Rebuilt.png")
    shop_boxes = [
        ("FeaturedBundle", (367, 366, 701, 590)),
        ("DailyGift", (48, 626, 128, 711)),
        ("EquipmentChest", (44, 782, 211, 958)),
        ("GemBundle", (493, 785, 659, 944)),
        ("GrowthElixir", (54, 1022, 197, 1212)),
        ("SkillBook", (493, 1026, 653, 1215)),
        ("DungeonTicket", (51, 1279, 194, 1473)),
        ("ExpBooster", (493, 1277, 668, 1469)),
    ]
    for name, box in shop_boxes:
        art = b.flood_remove_background(shop.crop(box), tolerance=38)
        save(normalize(art, (256, 256), 22), f"Shop/Product_{name}.png")

        if name == "GrowthElixir":
            potion = normalize(art, (64, 64), 10)
            save(potion, "Main/Potion_HP.png")
            blue = potion.copy()
            pixels = blue.load()
            for y in range(blue.height):
                for x in range(blue.width):
                    r, g, bl, a = pixels[x, y]
                    if a and r > g * 1.15 and r > bl * 1.15:
                        pixels[x, y] = (max(20, bl), min(170, g + 35), min(255, r), a)
            save(blue, "Main/Potion_MP.png")

    manifest = {
        "rule": "Only files in UnityReady are runtime sprites. All text and numbers use TMP.",
        "reference_screens": ["Main", "Equipment", "Dungeon", "Skill", "Shop"],
        "assembly": {
            "navigation": ["Common/Nav_Frame_Hex.png", "Common/Nav_Glyph_*.png", "TMP label", "optional Common/Badge_Notification.png"],
            "mail": ["Common/Icon_Mail.png", "optional Common/Badge_Notification.png"],
            "orb": ["Main/Orb_Frame.png", "Main/Orb_HP_Fill.png or Orb_MP_Fill.png", "TMP values"],
            "skill": ["Common frame", "Skill/Skill_*_Icon.png", "optional Common/Badge_SelectedCheck.png"],
            "dungeon_card": ["Common 9-slice panel", "Dungeon/Art_*.png", "Dungeon/Rewards/Reward_*.png", "TMP labels", "Unity Button"],
            "equipment_slot": ["Common slot frame", "Equipment/Items/Item_*.png", "TMP enhancement", "rarity/state overlays"],
            "shop_card": ["Common 9-slice panel", "Shop/Product_*.png", "TMP labels/prices", "Unity Button"],
        },
    }
    (OUT / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"Built {len(list(OUT.rglob('*.png')))} Unity-ready sprites")


if __name__ == "__main__":
    main()
