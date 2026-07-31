# Bright Theme UI sprites

This folder is generated from the approved `SevenKnights_Rebuilt` references.

- `Common/Frames`: canonical reusable frames. Use `Image.Type = Sliced`.
- `Common/Icons`: transparent navigation and utility icons.
- `Main`: screen-specific HUD sprites.
- `Equipment`: stat icons, panel composites, and 128×128 centered copies of every
  existing casual equipment item.
- `Dungeon`: dungeon thumbnails and reward icons.
- `Skill`: both framed sprites and frame-free icon variants.
- `Shop`: transparent product art where extraction is reliable.
- `Compatibility`: comparison sheet using the existing casual equipment icons.

Equipment artwork from the generated reference is intentionally not shipped.
Reuse `Assets/05_Resources/UI/Equipments/Item_*.png`; it is a better match for
the game's established cartoon rendering.

The importer at `Assets/Editor/BrightThemeSpriteImporter.cs` configures PNGs as
single UI sprites and applies borders to the canonical 9-slice frames.

Do not slice repeated panel crops from each screen. Build those areas from the
nine canonical files listed in `sprite_manifest.json`; this prevents duplicate
frames and keeps border thickness consistent at different resolutions.
