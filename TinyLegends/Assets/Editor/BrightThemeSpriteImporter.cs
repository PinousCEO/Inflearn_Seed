using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

internal sealed class BrightThemeSpriteImporter : AssetPostprocessor
{
    private const string Root = "Assets/05_Resources/UI/BrightTheme/";

    private static readonly Dictionary<string, Vector4> Borders =
        new(StringComparer.Ordinal)
        {
            ["Panel_Slate.png"] = new Vector4(12, 12, 12, 12),
            ["Panel_Slate_Translucent.png"] = new Vector4(12, 12, 12, 12),
            ["Panel_Light.png"] = new Vector4(12, 12, 12, 12),
            ["Chip_Dark.png"] = new Vector4(12, 12, 12, 12),
            ["Button_Coral.png"] = new Vector4(12, 12, 12, 12),
            ["Slot_Item.png"] = new Vector4(12, 12, 12, 12),
            ["Slot_Skill.png"] = new Vector4(12, 12, 12, 12),
            ["Panel_Dark.png"] = new Vector4(28, 28, 28, 28),
            ["Panel_Tooltip.png"] = new Vector4(32, 32, 32, 32),
            ["Button_Primary.png"] = new Vector4(24, 24, 24, 24),
            ["Button_Secondary.png"] = new Vector4(24, 24, 24, 24),
            ["Tab_Idle.png"] = new Vector4(24, 24, 24, 24),
            ["Tab_Selected.png"] = new Vector4(24, 24, 24, 24),
            ["Slot_Reward.png"] = new Vector4(24, 24, 24, 24),
            ["Row_Stat.png"] = new Vector4(24, 24, 24, 24),
            ["Panel_Stats.png"] = new Vector4(24, 24, 24, 24),
            ["Panel_Inventory.png"] = new Vector4(24, 24, 24, 24),
            ["Panel_Paperdoll.png"] = new Vector4(24, 24, 24, 24),
            ["Panel_ItemDetail.png"] = new Vector4(24, 24, 24, 24),
            ["Button_Action_Normal.png"] = new Vector4(24, 24, 24, 24),
            ["Button_Action_Pressed.png"] = new Vector4(24, 24, 24, 24),
            ["Button_Action_Disabled.png"] = new Vector4(24, 24, 24, 24),
            ["Button_Icon_Normal.png"] = new Vector4(20, 20, 20, 20),
            ["Button_Icon_Pressed.png"] = new Vector4(20, 20, 20, 20),
            ["CapacityBar.png"] = new Vector4(20, 20, 20, 20),
            ["Equipment_Tab_Normal.png"] = new Vector4(24, 24, 24, 24),
            ["Equipment_Tab_Selected.png"] = new Vector4(24, 24, 24, 24),
            ["Equipment_Slot_Normal.png"] = new Vector4(18, 18, 18, 18),
            ["Equipment_Slot_Armor.png"] = new Vector4(18, 18, 18, 18),
            ["Equipment_Slot_Selected.png"] = new Vector4(18, 18, 18, 18),
            ["Scrollbar_Track.png"] = new Vector4(12, 12, 12, 12),
            ["Scrollbar_Handle.png"] = new Vector4(12, 12, 12, 12),
            ["Panel_Tier.png"] = new Vector4(16, 16, 16, 16),
            ["Panel_SkillInfo.png"] = new Vector4(16, 16, 16, 16),
            ["SkillSlot_Normal.png"] = new Vector4(16, 16, 16, 16),
            ["SkillSlot_Selected.png"] = new Vector4(16, 16, 16, 16),
            ["SkillSlot_Locked.png"] = new Vector4(16, 16, 16, 16),
            ["LevelRequirement_Unlocked.png"] = new Vector4(24, 24, 24, 24),
            ["LevelRequirement_Locked.png"] = new Vector4(24, 24, 24, 24),
            ["Level_Badge.png"] = new Vector4(10, 10, 10, 10),
            ["Button_LevelUp_Normal.png"] = new Vector4(18, 18, 18, 18),
            ["Button_LevelUp_Pressed.png"] = new Vector4(18, 18, 18, 18),
            ["Button_LevelUp_Disabled.png"] = new Vector4(18, 18, 18, 18),
            ["Button_Reset.png"] = new Vector4(16, 16, 16, 16),
        };

    public override uint GetVersion() => 2;

    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(Root, StringComparison.Ordinal) ||
            !assetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100f;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.crunchedCompression = false;
        // Several recreated HUD layers are authored at 1024 px. A blanket
        // 512 limit silently downscaled them during import and blurred them.
        importer.maxTextureSize = 2048;
        if (Borders.TryGetValue(Path.GetFileName(assetPath), out var border))
        {
            importer.spriteBorder = border;
        }
    }
}
