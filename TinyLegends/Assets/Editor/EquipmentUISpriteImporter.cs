using UnityEditor;
using UnityEngine;

internal sealed class EquipmentUISpriteImporter : AssetPostprocessor
{
    private const string UiFramesPath =
        "Assets/05_Resources/UI/Equipments/UIFrames/";
    private const string UiIconsPath =
        "Assets/05_Resources/UI/Equipments/UIIcons/";

    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(UiFramesPath) &&
            !assetPath.StartsWith(UiIconsPath))
        {
            return;
        }

        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100f;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.Compressed;
    }
}
