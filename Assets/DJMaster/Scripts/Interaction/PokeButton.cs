using System;
using UnityEngine;

namespace DJMaster
{
    // A push button you poke with a fingertip. It only fires when the finger comes in from above,
    // so brushing past it on the way to another control doesn't trigger it.
    public class PokeButton : DJControl
    {
        public Vector2 size = new Vector2(0.032f, 0.032f); // local x/z footprint
        public float hoverHeight = 0.03f;
        public float pressDepth = 0.007f;
        public Transform cap;
        public Renderer capRenderer;
        public Color offColor = new Color(0.12f, 0.12f, 0.14f);
        public Color onColor = new Color(0.1f, 0.9f, 0.5f);
        [Tooltip("How much of onColor the cap shows when it's off (ColorSafeView keeps it when it swaps colors).")]
        public float offTint = 0.3f;
        public float cooldown = 0.25f;
        [Tooltip("LED rim around the cap: dim in the button's color, full when lit, flashing on a press.")]
        public Renderer rim;
        [Tooltip("Additive halo under the button, only on while it flashes from a press.")]
        public Renderer halo;

        public bool Lit { get; set; }
        public event Action Pressed;

        Pointer armedBy, pressedBy;
        float lastPress = -10f;
        float capOffset, flash;
        Vector3 capRest;
        MaterialPropertyBlock block;

        void Awake()
        {
            if (cap != null) capRest = cap.localPosition;
        }

        protected override void OnDisable()
        {
            pressedBy = armedBy = null;
            base.OnDisable();
        }

        void Update()
        {
            const float margin = 0.006f;
            float depth = 0f;

            if (pressedBy != null)
            {
                var lp = Local(pressedBy);
                if (!pressedBy.Active || lp.y > -pressDepth * 0.4f || !Inside(lp, margin * 2))
                {
                    Release(pressedBy);
                    pressedBy = null;
                }
                else depth = pressDepth;
            }

            if (pressedBy == null)
            {
                Pointer nextArmed = null;
                foreach (var p in Pointers.All)
                {
                    if (!p.IsFree(this)) continue;
                    var lp = Local(p);
                    if (!Inside(lp, margin)) continue;

                    bool fromAbove = p == armedBy || p.DirectPress;
                    if (lp.y < hoverHeight && lp.y > 0) nextArmed = p;
                    // If the finger came in from above it stays ready while it pushes down through the surface,
                    // so a slow poke still works. After a press it has to go back above the surface first
                    else if (lp.y <= 0 && p == armedBy) nextArmed = p;
                    if (lp.y <= -pressDepth && fromAbove && Time.time - lastPress > cooldown)
                    {
                        pressedBy = p;
                        nextArmed = null;
                        Engage(p);
                        lastPress = Time.time;
                        depth = pressDepth;
                        Sfx.Click();
                        flash = 1f;
                        Pressed?.Invoke();
                        break;
                    }
                    if (lp.y < 0 && lp.y > -pressDepth) depth = Mathf.Max(depth, -lp.y);
                }
                armedBy = nextArmed;
            }

            capOffset = Mathf.MoveTowards(capOffset, depth, Time.deltaTime * 0.2f);
            if (cap != null) cap.localPosition = capRest - Vector3.up * capOffset;
        }

        bool Inside(Vector3 lp, float margin) =>
            Mathf.Abs(lp.x) < size.x * 0.5f + margin && Mathf.Abs(lp.z) < size.y * 0.5f + margin && lp.y < hoverHeight;

        // Lets the lessons and the PlayMode tests press a button without a hand.
        public void SimulatePress() { flash = 1f; Pressed?.Invoke(); }

        protected override void LateUpdate()
        {
            base.LateUpdate();
            flash = Mathf.MoveTowards(flash, 0f, Time.deltaTime / 0.35f);
            // How hard it shines from the press: full while held, then fading out after the finger lifts
            float shine = pressedBy != null ? 1f : flash * flash;
            var bright = Color.Lerp(onColor, Color.white, 0.55f);
            block ??= new MaterialPropertyBlock();
            if (capRenderer != null)
            {
                var c = Lit ? onColor : offColor;
                c = Color.Lerp(c, bright, shine * 0.8f);
                block.SetColor("_Color", c);
                // Rubber pads (a lit material) glow from inside in their color; unlit caps just ignore this
                block.SetColor("_EmissionColor", c * (Lit || shine > 0 ? 0.85f : 0.08f));
                capRenderer.SetPropertyBlock(block);
            }
            if (rim != null)
            {
                var r = Lit ? onColor : onColor * 0.32f;
                r = Color.Lerp(r, bright, shine);
                r.a = 1f;
                block.SetColor("_Color", r);
                rim.SetPropertyBlock(block);
            }
            if (halo != null)
            {
                halo.enabled = shine > 0.01f;
                if (halo.enabled)
                {
                    var h = onColor * (0.75f * shine);
                    h.a = 1f;
                    block.SetColor("_Color", h);
                    halo.SetPropertyBlock(block);
                }
            }
        }
    }
}
