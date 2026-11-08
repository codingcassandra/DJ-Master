using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace DJMaster
{
    // Applies color-blind mode to everything under this object: the safe palette on every color-coded button, ring,
    // skirt and label, and each control's shape in front of its label (e.g. "● PLAY"). It works from the colors baked
    // into the scene, so the scene doesn't need rebuilding. Call Init once after adding it.
    public class ColorSafeView : MonoBehaviour
    {
        class ButtonColors { public PokeButton button; public Color on, off; public BoardColors.Family family; public float brightness; }
        class Tinted { public Renderer renderer; public Color original; public BoardColors.Family family; public float brightness; }
        class Label { public TextMeshPro mesh; public Color color; public string text; public float size; public BoardColors.Family colorFamily, shape; public float brightness; }

        readonly List<ButtonColors> buttons = new();
        readonly List<Tinted> tinted = new();
        readonly List<Label> labels = new();
        MaterialPropertyBlock block;
        bool ready;

        public void Init()
        {
            if (ready) return;
            ready = true;
            Collect();
            BoardColors.Changed += Apply;
            Apply();
        }

        void OnDestroy() => BoardColors.Changed -= Apply;

        void Collect()
        {
            // Renderers that other scripts recolor every frame are left to them.
            var skip = new HashSet<Renderer>();
            foreach (var c in GetComponentsInChildren<DJControl>(true))
            {
                if (c.glow != null) skip.Add(c.glow);
                if (c is PokeButton pb && pb.capRenderer != null) skip.Add(pb.capRenderer);
            }
            foreach (var d in GetComponentsInChildren<DeckDisplay>(true))
            {
                foreach (var r in d.beatLights) if (r != null) skip.Add(r);
                if (d.levelFill != null) skip.Add(d.levelFill);
            }
            foreach (var m in GetComponentsInChildren<SyncMeter>(true))
            {
                if (m.lockLight != null) skip.Add(m.lockLight);
                if (m.energyFill != null) skip.Add(m.energyFill);
            }

            foreach (var b in GetComponentsInChildren<PokeButton>(true))
            {
                var f = BoardColors.FamilyOf(b.onColor, out float k);
                if (f != BoardColors.Family.None)
                    buttons.Add(new ButtonColors { button = b, on = b.onColor, off = b.offColor, family = f, brightness = k });
            }

            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (skip.Contains(r) || r is not MeshRenderer || r.GetComponent<TMP_Text>() != null) continue;
                var mat = r.sharedMaterial;
                if (mat == null || !mat.HasProperty("_Color")) continue;
                var f = BoardColors.FamilyOf(mat.color, out float k);
                if (f != BoardColors.Family.None)
                    tinted.Add(new Tinted { renderer = r, original = mat.color, family = f, brightness = k });
            }

            foreach (var tm in GetComponentsInChildren<TextMeshPro>(true))
            {
                var colorFamily = BoardColors.FamilyOf(tm.color, out float k);
                // Only the fixed labels of color-coded board controls get a shape; panel and crate buttons change text at runtime.
                var control = tm.GetComponentInParent<DJControl>(true);
                var shape = control != null ? BoardColors.FamilyOf(control.id) : BoardColors.Family.None;
                if (string.IsNullOrEmpty(tm.text)) shape = BoardColors.Family.None;
                if (colorFamily == BoardColors.Family.None && shape == BoardColors.Family.None) continue;
                labels.Add(new Label { mesh = tm, color = tm.color, text = tm.text, size = tm.fontSize, colorFamily = colorFamily, brightness = k, shape = shape });
            }
        }

        static Color Recolor(BoardColors.Family f, float brightness, float alpha)
        {
            var c = BoardColors.Of(f) * brightness;
            c.a = alpha;
            return c;
        }

        void Apply()
        {
            bool safe = BoardColors.ColorSafe;

            foreach (var b in buttons)
            {
                if (b.button == null) continue;
                if (!safe) { b.button.onColor = b.on; b.button.offColor = b.off; continue; }
                var on = Recolor(b.family, b.brightness, b.on.a);
                b.button.onColor = on;
                b.button.offColor = Color.Lerp(new Color(0.1f, 0.1f, 0.11f), on, 0.3f); // same dim tint the scene uses
            }

            block ??= new MaterialPropertyBlock();
            foreach (var t in tinted)
            {
                if (t.renderer == null) continue;
                if (!safe) { t.renderer.SetPropertyBlock(null); continue; }
                block.Clear();
                block.SetColor("_Color", Recolor(t.family, t.brightness, t.original.a));
                t.renderer.SetPropertyBlock(block);
            }

            foreach (var l in labels)
            {
                if (l.mesh == null) continue;
                if (l.colorFamily != BoardColors.Family.None)
                    l.mesh.color = safe ? Recolor(l.colorFamily, l.brightness, l.color.a) : l.color;
                if (l.shape == BoardColors.Family.None) continue;
                if (safe)
                {
                    // Shrink a little so the extra shape still fits on small pads.
                    int len = l.text.Length;
                    l.mesh.fontSize = l.size * Mathf.Max(0.75f, len / (len + 2f));
                    l.mesh.text = $"{BoardColors.Shape(l.shape)} {l.text}";
                }
                else
                {
                    l.mesh.fontSize = l.size;
                    l.mesh.text = l.text;
                }
            }
        }
    }
}
