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
            ["Tab_Idle.png"] = new Vector4(12, 12, 12, 12),
            ["Tab_Selected.png"] = new Vector4(12, 12, 12, 12),
            ["Slot_Item.png"] = new Vector4(12, 12, 12, 12),
            ["Slot_Skill.png"] = new Vector4(12, 12, 12, 12),
        };

    public override uint GetVersion() => 1;

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
        importer.textureCompression = TextureImporterCompression.Compressed;
        importer.crunchedCompression = false;
        importer.maxTextureSize = assetPath.Contains("/Dungeon/") ? 1024 : 512;

        if (Borders.TryGetValue(Path.GetFileName(assetPath), out var border))
        {
            importer.spriteBorder = border;
        }
    }
}
