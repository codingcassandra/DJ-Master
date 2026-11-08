using System;
using UnityEngine;

namespace DJMaster
{
    // Base for everything you can touch on the board. Controls work in their own local space:
    // +Y points out of the surface toward the hand, and the touch surface sits at local y = 0.
    [DefaultExecutionOrder(-50)]
    public abstract class DJControl : MonoBehaviour
    {
        public string id;
        public string label;
        [Tooltip("Pulsing outline the lessons use to point at this control.")]
        public Renderer glow;
        public Color glowColor = new Color(1f, 0.85f, 0.2f, 0.9f);

        public bool Highlighted { get; set; }
        public bool Touched { get; protected set; }

        public static event Action<DJControl> AnyTouched;

        MaterialPropertyBlock block;

        protected Vector3 Local(Pointer p) => transform.InverseTransformPoint(p.Position);

        protected void Engage(Pointer p)
        {
            p.Owner = this;
            if (!Touched) AnyTouched?.Invoke(this);
            Touched = true;
        }

        // Lets the PlayMode tests touch a control without a hand.
        public void SimulateTouch() => AnyTouched?.Invoke(this);

        protected void Release(Pointer p)
        {
            if (p != null && p.Owner == this) p.Owner = null;
            Touched = false;
        }

        // A control can be hidden mid-press (a menu button that closes its own menu). Hand the finger back,
        // or every other control keeps seeing it as busy.
        protected virtual void OnDisable()
        {
            foreach (var p in Pointers.All)
                if (p.Owner == this) p.Owner = null;
            Touched = false;
        }

        protected virtual void LateUpdate()
        {
            if (glow == null) return;
            glow.enabled = Highlighted;
            if (!Highlighted) return;
            block ??= new MaterialPropertyBlock();
            var c = glowColor;
            c.a *= 0.45f + 0.45f * Mathf.Sin(Time.time * 6f);
            block.SetColor("_Color", c);
            glow.SetPropertyBlock(block);
        }
    }
}
