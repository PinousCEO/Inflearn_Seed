#!/usr/bin/env python3
"""Extract only new stat glyphs from the 1080x1920 detail reference."""
from pathlib import Path
from PIL import Image

from build_seven_knights_ui_assets import flood_remove_background

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "Assets/05_Resources/UI/BrightReferences/Equipment_DetailedStats_Reference.png"
OUT = ROOT / "Assets/05_Resources/UI/BrightTheme/Recreated/Equipment/Icons"

# Exact source boxes in the approved 1080x1920 reference. Existing Attack,
# Defense, Health, Critical, AttackSpeed and MagicFind glyphs are omitted.
BOXES = {
    "Icon_Stat_Mana": (545, 496, 594, 551),
    "Icon_Stat_CooldownReduction": (543, 714, 595, 771),
    "Icon_Stat_SkillDamage": (227, 780, 281, 837),
    "Icon_Stat_ArmorPenetration": (542, 780, 596, 837),
    "Icon_Stat_FireResistance": (226, 932, 281, 989),
    "Icon_Stat_LightningResistance": (542, 932, 596, 989),
    "Icon_Stat_ColdResistance": (226, 1005, 281, 1063),
    "Icon_Stat_ChaosResistance": (542, 1005, 596, 1063),
    "Icon_Stat_ResourceCostReduction": (542, 1220, 596, 1278),
    "Icon_Stat_MovementSpeed": (226, 1288, 281, 1347),
    "Icon_Source_Equipment": (479, 1434, 538, 1495),
    "Icon_Source_Passive": (684, 1434, 747, 1495),
}


def normalize(icon: Image.Image) -> Image.Image:
    bbox = icon.getchannel("A").getbbox()
    if not bbox:
        raise RuntimeError("Extraction produced an empty icon")
    art = icon.crop(bbox)
    art.thumbnail((48, 48), Image.Resampling.LANCZOS)
    result = Image.new("RGBA", (64, 64), (0, 0, 0, 0))
    result.alpha_composite(art, ((64 - art.width) // 2, (64 - art.height) // 2))
    return result


def main():
    source = Image.open(SOURCE).convert("RGBA")
    if source.size != (1080, 1920):
        raise RuntimeError(f"Expected 1080x1920 reference, got {source.size}")
    OUT.mkdir(parents=True, exist_ok=True)
    for name, box in BOXES.items():
        crop = source.crop(box)
        icon = flood_remove_background(crop, tolerance=30)
        normalize(icon).save(OUT / f"{name}.png", optimize=True)
    print(f"Extracted {len(BOXES)} reference-matched icons under {OUT}")


if __name__ == "__main__":
    main()
