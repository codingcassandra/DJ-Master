using System;
using UnityEngine;

namespace DJMaster
{
    // A rotary knob. Touch the top with a fingertip and slide away from you (or to the right) to turn it up,
    // toward you (or left) to turn it down. Small fingertip motions map to a full sweep.
    public class Knob : DJControl
    {
        public float radius = 0.017f;
        public Transform indicator;            // rotates -135..135 degrees about local Y
        [Range(0, 1)] public float value = 0.5f;
        public float defaultValue = 0.5f;
        [Tooltip("Metres of finger travel for a full sweep.")]
        public float throw01 = 0.07f;

        public event Action<float> Changed;

        Pointer holder;
        Vector3 lastLocal;
        float touchStart, valueAtTouch;
        float lastTapEnd = -10f;

        void Update()
        {
            if (holder != null)
            {
                var lp = Local(holder);
                Vector2 flat = new Vector2(lp.x, lp.z);
                if (!holder.Active || lp.y > 0.045f || lp.y < -0.035f || flat.magnitude > radius + 0.05f)
                {
                    // Only a short, still touch that ends with the finger lifting off counts as a tap. Hand tracking
                    // dropping out for a moment also lets go of the knob, and that must never count toward a double-tap.
                    bool liftedOff = holder.Active || holder.DirectPress;
                    bool tap = liftedOff && Time.time - touchStart < 0.3f && Mathf.Abs(value - valueAtTouch) < 0.03f;
                    lastTapEnd = tap ? Time.time : -10f;
                    Release(holder); holder = null;
                }
                else
                {
                    Vector3 d = lp - lastLocal;
                    lastLocal = lp;
                    SetValue(value + (d.z + d.x) / throw01);
                }
            }

            if (holder == null)
            {
                foreach (var p in Pointers.All)
                {
                    if (!p.IsFree(this)) continue;
                    var lp = Local(p);
                    if (lp.y > 0.012f || lp.y < -0.02f || new Vector2(lp.x, lp.z).magnitude > radius + 0.008f) continue;
                    holder = p;
                    lastLocal = lp;
                    Engage(p);
                    // Double-tap resets to center, the "home" position DJs return EQs to.
                    if (Time.time - lastTapEnd < 0.35f) { SetValue(defaultValue); lastTapEnd = -10f; }
                    touchStart = Time.time;
                    valueAtTouch = value;
                    break;
                }
            }

            if (indicator != null) indicator.localRotation = Quaternion.Euler(0, Mathf.Lerp(-135f, 135f, value), 0);
        }

        // Editor mouse wheel support.
        public void Nudge(float delta) => SetValue(value + delta);

        public void SetValue(float v)
        {
            v = Mathf.Clamp01(v);
            if (Mathf.Approximately(v, value)) return;
            value = v;
            Changed?.Invoke(value);
        }
    }
}
