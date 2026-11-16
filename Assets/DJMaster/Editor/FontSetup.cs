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

        static TMP_FontAsset Make(string ttfPath, string path, string name, TMP_FontAsset bold)
        {
            var ttf = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
            if (ttf == null) throw new System.Exception("Font file missing: " + ttfPath);
            var asset = TMP_FontAsset.CreateFontAsset(ttf, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            asset.name = name;
            asset.TryAddCharacters(Characters(), out string missing);
            if (!string.IsNullOrEmpty(missing)) Debug.Log($"[DJMaster] {name} has no glyph for: {missing} (LiberationSans covers them)");
            asset.atlasPopulationMode = AtlasPopulationMode.Static;

            AssetDatabase.CreateAsset(asset, path);
            asset.atlasTextures[0].name = name + " Atlas";
            AssetDatabase.AddObjectToAsset(asset.atlasTextures[0], asset);
            asset.material.name = name + " Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);

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
