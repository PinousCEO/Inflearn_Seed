# Bright Theme UI sprites

This folder is generated from the approved `SevenKnights_Rebuilt` references.

- `Common/Frames`: canonical reusable frames. Use `Image.Type = Sliced`.
- `Common/Icons`: transparent navigation and utility icons.
- `Main`: screen-specific HUD sprites.
- `Equipment`: stat icons, panel composites, and 128×128 centered copies of every
  existing casual equipment item.
- `Dungeon`: dungeon thumbnails and reward icons.
- `Skill`: normalized 128x128 runtime icons, a separate selected-state overlay,
  and padded `Framed` preview crops. Build runtime slots from
  `Common/Frames/Slot_Skill.png` + `Skill_*_Icon.png` +
  `Overlay_SelectedCheck.png`; do not use `Framed` previews as buttons.
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

Files ending in `_Composite` and `_Framed` are visual references, not reusable
runtime controls. They contain multiple baked layers. Runtime UI must be
assembled from the canonical frames, normalized icons, text components, and
state overlays.

## Runtime assets

Use files under `Recreated` for Unity prefabs. Files outside that folder are
source crops or visual previews and must not be assigned directly to controls.

Use the individual PNG files in each `Recreated` subfolder. Each file is one
Unity UI sprite. There is no duplicate Multiple/SpriteSheet version.

Artwork in `Common`, `Main`, `Skill`, `Dungeon`, and `Shop` is restored from
the five approved reference screens. It is not regenerated in a different
rendering style. Equipment art uses the preserved 75 PNG files under
`BrightTheme/Equipment/Items`.

Slider layers are under `Recreated/Sliders`. Each set uses identical canvas
dimensions so the layers align without offsets:

- Stage: `Stage_Background` + `Stage_Fill` + `Stage_Frame` (512x64)
- EXP: `EXP_Background` + `EXP_Fill` + `EXP_Frame` (512x32)
- HP/MP: `Orb_Background` + `Orb_HP_Fill` or `Orb_MP_Fill` + `Orb_Frame`
  (256x256)

Use the rectangular Fill as the Slider `Fill Rect`. Use orb fills with
`Image.Type = Filled` for radial or vertical depletion.

- Bottom navigation: `Nav_Frame_Hex` + `Nav_Glyph_*` + TMP label.
- Mail: `Icon_Mail` + optional `Badge_Notification`.
- HP/MP: `Orb_Frame` + the corresponding `Orb_*_Fill` + TMP value labels.
- Currency: `Currency_Gold`/`Currency_Gem` + TMP quantity.
- Potions: `Slot_Skill` + `Potion_HP`/`Potion_MP` + TMP cooldown.
- Stage progress: `StageProgress_Frame` + filled-width
  `StageProgress_Fill`; stage/wave values are TMP.
- Quick buttons: a canonical circular/button frame + `Quick_Gift` or
  `Quick_Log` + optional `Badge_Notification`.
- Never bake Korean labels, counters, cooldowns, or notification state into a
  reusable sprite.
