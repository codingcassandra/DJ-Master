using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace DJMaster.EditorTools
{
    // The one font (#32): Inter (SIL Open Font License, Assets/DJMaster/Fonts/Inter-OFL.txt) as a TextMeshPro font asset,
    // with a bold weight and LiberationSans behind it for symbols Inter doesn't have (the color-blind shapes).
    // Static atlases, pre-filled with every character the game uses, so pressing Play never rewrites the asset.
    // It's also the TMP default, so text made at runtime gets it too.
    public static class FontSetup
    {
        const string RegularTtf = "Assets/DJMaster/Fonts/Inter-Regular.ttf";
        const string BoldTtf = "Assets/DJMaster/Fonts/Inter-Bold.ttf";
        const string BoldPath = "Assets/DJMaster/Fonts/Inter Bold SDF.asset";
        const string FallbackPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
        // The color-blind shapes, baked from LiberationSans into their own small static atlas (#55). LiberationSans SDF
        // doesn't have them, it only reached them through its dynamic fallback, which writes glyphs at runtime
        const string ShapesTtf = "Assets/TextMesh Pro/Fonts/LiberationSans.ttf";
        const string ShapesPath = "Assets/DJMaster/Fonts/Shapes SDF.asset";
        public const string ShapeCharacters = "◄►●■♦♥";

        // ASCII, Latin-1 and the few symbols the game prints
        static string Characters()
        {
            var sb = new StringBuilder();
            for (int c = 32; c < 127; c++) sb.Append((char)c);
            for (int c = 160; c < 256; c++) sb.Append((char)c);
            sb.Append("’‘“”–—…•·→←");
            return sb.ToString();
        }

        [MenuItem("DJ Master/Rebuild Font")]
        static void Rebuild()
        {
            AssetDatabase.DeleteAsset(Style.FontPath);
            AssetDatabase.DeleteAsset(BoldPath);
            AssetDatabase.DeleteAsset(ShapesPath);
            Ensure();
        }

        public static TMP_FontAsset Ensure()
        {
            var regular = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Style.FontPath);
            if (regular == null)
            {
                var bold = Make(BoldTtf, BoldPath, "Inter Bold SDF", null);
                regular = Make(RegularTtf, Style.FontPath, "Inter SDF", bold);
            }
            EnsureShapes(regular);
            EnsureShapes(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BoldPath));
            var settings = new SerializedObject(TMP_Settings.instance);
            var prop = settings.FindProperty("m_defaultFontAsset");
            if (prop != null && prop.objectReferenceValue != regular)
            {
                prop.objectReferenceValue = regular;
                settings.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(TMP_Settings.instance);
                AssetDatabase.SaveAssets();
            }
            return regular;
        }

        // Puts the shapes atlas first in a font's fallbacks, making it if needed
        static void EnsureShapes(TMP_FontAsset font)
        {
            if (font == null) return;
            var shapes = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ShapesPath);
            if (shapes == null) shapes = Bake(ShapesTtf, ShapesPath, "Shapes SDF", ShapeCharacters, 256);
            var list = font.fallbackFontAssetTable ?? new List<TMP_FontAsset>();
            if (list.Count > 0 && list[0] == shapes) return;
            list.Remove(shapes);
            list.Insert(0, shapes);
            font.fallbackFontAssetTable = list;
            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssets();
        }

        // A static font asset holding just these characters, saved with its atlas and material
        static TMP_FontAsset Bake(string ttfPath, string path, string name, string characters, int atlasSize)
        {
            var ttf = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
            if (ttf == null) throw new System.Exception("Font file missing: " + ttfPath);
            var asset = TMP_FontAsset.CreateFontAsset(ttf, 90, 9, GlyphRenderMode.SDFAA, atlasSize, atlasSize, AtlasPopulationMode.Dynamic, true);
            asset.name = name;
            asset.TryAddCharacters(characters, out string missing);
            if (!string.IsNullOrEmpty(missing)) Debug.Log($"[DJMaster] {name} has no glyph for: {missing} (the fallbacks cover them)");
            asset.atlasPopulationMode = AtlasPopulationMode.Static;

            AssetDatabase.CreateAsset(asset, path);
            asset.atlasTextures[0].name = name + " Atlas";
            AssetDatabase.AddObjectToAsset(asset.atlasTextures[0], asset);
            asset.material.name = name + " Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            return asset;
        }

        static TMP_FontAsset Make(string ttfPath, string path, string name, TMP_FontAsset bold)
        {
            var asset = Bake(ttfPath, path, name, Characters(), 1024);
            var fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FallbackPath);
            if (fallback != null) asset.fallbackFontAssetTable = new List<TMP_FontAsset> { fallback };
            if (bold != null)
            {
                asset.fontWeightTable[7].regularTypeface = bold; // <b> and FontStyles.Bold use the real bold weight (700)
            }
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            return asset;
        }
    }
}
