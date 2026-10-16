#if UNITY_EDITOR
using StarPixelSurge.Framework;
using UnityEditor;
using UnityEngine;

namespace StarPixelSurge.EditorTools
{
    /// <summary>
    /// 像素美术导入设置工具。把 <c>Assets/11_Res/Resources/Sprites</c> 下的贴图统一为
    /// 点采样、无压缩的像素画导入参数，并在新贴图导入时自动应用，避免逐张手动配置。
    /// 目录与命名约定见 <c>Assets/11_Res/Sprites/README.md</c>。
    /// </summary>
    public static class SpriteImportSettings
    {
        private const string SpritesFolder = "Assets/11_Res/Resources/Sprites";
        private const int PixelsPerUnit = 32;

        [MenuItem("StarPixelSurge/Assets/Apply Sprite Import Settings")]
        public static void ApplyAll()
        {
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { SpritesFolder });
            var applied = 0;
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (Apply(path))
                {
                    applied++;
                }
            }

            AssetDatabase.Refresh();
            SpriteCatalog.Clear();
            Debug.Log($"[StarPixelSurge] Sprite import settings applied to {applied} texture(s) under {SpritesFolder}.");
        }

        /// <summary>应用像素画导入参数；成功修改返回 true。</summary>
        public static bool Apply(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath) || !assetPath.StartsWith(SpritesFolder))
            {
                return false;
            }

            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                return false;
            }

            if (importer.textureType == TextureImporterType.Sprite &&
                importer.spriteImportMode == SpriteImportMode.Single &&
                importer.filterMode == FilterMode.Point &&
                importer.textureCompression == TextureImporterCompression.Uncompressed &&
                importer.spritePixelsPerUnit == PixelsPerUnit)
            {
                return false;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            return true;
        }
    }

    /// <summary>新贴图落入 Sprites 目录时自动套用像素画导入参数。</summary>
    public sealed class SpriteImportPostprocessor : AssetPostprocessor
    {
        private const string SpritesFolder = "Assets/11_Res/Resources/Sprites";

        private void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').StartsWith(SpritesFolder))
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 32;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
        }
    }
}
#endif
