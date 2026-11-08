using System;
using UnityEngine;

namespace DJMaster
{
    // A slide fader. Touch the cap with a fingertip and drag it along the slot.
    // The slot runs along local Z (or local X when horizontal), centered on the transform.
    public class Fader : DJControl
    {
        public bool horizontal;
        public float length = 0.09f;
        public float width = 0.03f;
        public Transform cap;
        [Range(0, 1)] public float value;
        [Tooltip("Value the cap clicks into when you pass near it, e.g. 0.5 for a tempo fader.")]
        public float detent = -1f;
        [Tooltip("Cap travel per unit of finger travel. Below 1 gives fine control, e.g. for matching tempo.")]
        public float dragRatio = 1f;

        public event Action<float> Changed;

        Pointer holder;
        float lastAxis;

        float Axis(Vector3 lp) => horizontal ? lp.x : lp.z;
        float Across(Vector3 lp) => horizontal ? lp.z : lp.x;
        float CapPos => (value - 0.5f) * length;

        protected override void OnDisable()
        {
            holder = null;
            base.OnDisable();
        }

        void Update()
        {
            if (holder != null)
            {
                var lp = Local(holder);
                bool lost = !holder.Active || lp.y > 0.05f || lp.y < -0.04f || Mathf.Abs(Across(lp)) > width * 1.5f
                            || Mathf.Abs(Axis(lp)) > length * 0.5f + 0.04f;
                if (lost) { Release(holder); holder = null; }
                else
                {
                    float a = Axis(lp);
                    float v = value + (a - lastAxis) * dragRatio / length;
                    lastAxis = a;
                    // Snap into the detent when the cap crosses it.
                    if (detent >= 0 && (value - detent) * (v - detent) < 0) v = detent;
                    SetValue(v);
                }
            }

            if (holder == null)
            {
                foreach (var p in Pointers.All)
                {
                    if (!p.IsFree(this)) continue;
                    var lp = Local(p);
                    if (lp.y > 0.022f || lp.y < -0.02f || Mathf.Abs(Across(lp)) > width * 0.5f + 0.006f) continue;
                    float a = Axis(lp);
                    if (Mathf.Abs(a) > length * 0.5f + 0.012f) continue;
                    // Fingers must find the cap; the mouse may click anywhere in the slot to jump there.
                    bool onCap = Mathf.Abs(a - CapPos) < 0.02f;
                    if (!onCap && !p.DirectPress) continue;
                    if (!onCap && dragRatio >= 1f) SetValue(a / length + 0.5f);
                    holder = p;
                    lastAxis = a;
                    Engage(p);
                    break;
                }
            }

            if (cap != null)
                cap.localPosition = horizontal ? new Vector3(CapPos, cap.localPosition.y, 0) : new Vector3(0, cap.localPosition.y, CapPos);
        }

        public void SetValue(float v)
        {
            v = Mathf.Clamp01(v);
            if (Mathf.Approximately(v, value)) return;
            value = v;
            Changed?.Invoke(value);
        }
    }
}
