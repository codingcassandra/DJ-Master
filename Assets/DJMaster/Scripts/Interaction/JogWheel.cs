using UnityEngine;

namespace DJMaster
{
    // The big platter. Rest a fingertip on it and spin to nudge the track faster or slower (or scrub when paused).
    // The platter turns once per bar, and its white marker points straight ahead on beat 1,
    // so two platters with aligned markers are two tracks in phase.
    public class JogWheel : DJControl
    {
        public Deck deck;
        public float radius = 0.07f;
        public Transform platter;

        public float Spin { get; private set; } // revolutions per second from the finger, for the lessons

        Pointer holder;
        float lastAngle;

        protected override void OnDisable()
        {
            holder = null;
            base.OnDisable();
        }

        void Update()
        {
            float revs = 0f;
            if (holder != null)
            {
                var lp = Local(holder);
                Vector2 flat = new Vector2(lp.x, lp.z);
                if (!holder.Active || lp.y > 0.035f || lp.y < -0.03f || flat.magnitude > radius + 0.03f)
                {
                    Release(holder); holder = null;
                }
                else if (flat.magnitude > 0.012f) // near the hub the angle is meaningless
                {
                    float angle = Mathf.Atan2(flat.y, flat.x) * Mathf.Rad2Deg;
                    // Clockwise seen from above (decreasing angle) = forward, like a record.
                    revs = -Mathf.DeltaAngle(lastAngle, angle) / 360f;
                    lastAngle = angle;
                }
            }

            if (holder == null)
            {
                foreach (var p in Pointers.All)
                {
                    if (!p.IsFree(this)) continue;
                    var lp = Local(p);
                    var flat = new Vector2(lp.x, lp.z);
                    if (lp.y > 0.008f || lp.y < -0.02f || flat.magnitude > radius) continue;
                    holder = p;
                    lastAngle = Mathf.Atan2(flat.y, flat.x) * Mathf.Rad2Deg;
                    Engage(p);
                    break;
                }
            }

            Spin = Time.deltaTime > 0 ? revs / Time.deltaTime : 0;
            if (holder != null && deck != null) deck.Jog(revs, Time.deltaTime);

            if (platter != null && deck != null)
                platter.localRotation = Quaternion.Euler(0, (float)(deck.Beat % TrackSynth.BeatsPerBar) * 90f, 0);
        }
    }
}
