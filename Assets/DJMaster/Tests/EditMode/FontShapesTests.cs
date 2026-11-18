using NUnit.Framework;
using TMPro;
using UnityEditor;

namespace DJMaster.Tests
{
    // The color-blind shapes aren't in Inter, so they come from the baked shapes atlas behind it (#55)
    public class FontShapesTests
    {
        [Test]
        public void EveryShapeHasAGlyph()
        {
            foreach (var path in new[] { Style.FontPath, "Assets/DJMaster/Fonts/Inter Bold SDF.asset" })
            {
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                Assert.IsNotNull(font, path);
                Assert.IsNotNull(font.fallbackFontAssetTable, path + " has no fallbacks");
                Assert.IsNotEmpty(font.fallbackFontAssetTable, path + " has no fallbacks");
                var shapes = font.fallbackFontAssetTable[0];
                Assert.AreEqual(AtlasPopulationMode.Static, shapes.atlasPopulationMode, "a static atlas, so nothing is written at runtime");
                foreach (char c in "◄►●■♦♥")
                    Assert.IsTrue(shapes.characterLookupTable.ContainsKey(c), $"{path}: no glyph for {c}");
            }
        }
    }
}
