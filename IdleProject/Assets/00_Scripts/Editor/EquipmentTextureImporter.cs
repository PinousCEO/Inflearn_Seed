using UnityEditor;
using UnityEngine;

namespace IdleBattle.Editor
{
    /// <summary>
    /// Keeps equipment icons and rarity frames lightweight and UI-ready.
    /// Changing the version below forces Unity to reimport matching textures.
    /// </summary>
    public sealed class EquipmentTextureImporter : AssetPostprocessor
    {
        private const string EquipmentUiPath = "Assets/05_Resources/UI/Equipments/";

        public override uint GetVersion()
        {
            return 1;
        }

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(EquipmentUiPath) ||
                !assetPath.EndsWith(".png"))
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
            importer.maxTextureSize = 256;
        }
    }
}
