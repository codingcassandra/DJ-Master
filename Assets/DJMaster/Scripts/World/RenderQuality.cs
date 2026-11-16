using UnityEngine;
using UnityEngine.XR;

namespace DJMaster
{
    // Sharp rendering on the Quest (#33). Starts at 1.2x eye resolution with foveation allowed up to Low (dynamic, so the
    // headset only uses it when it needs the time), then keeps the highest resolution that holds the frame rate:
    // if the average stays under target for a few seconds, it steps down 0.1 at a time, never below 1.0.
    // No post effects anywhere: the glow is emissive materials and additive sprites. 4x MSAA is set in Quality settings.
    // Only runs with a headset; the editor keeps its own settings.
    public class RenderQuality : MonoBehaviour
    {
        public float startScale = 1.2f, minScale = 1.0f, step = 0.1f;
        public float targetFps = 72f;
        [Tooltip("Seconds of frames averaged before deciding to step down.")]
        public float window = 3f;
        [Tooltip("Below this share of the target frame rate, on average, it steps down.")]
        public float tolerance = 0.95f;

        public float Scale { get; private set; } = 1f;

        float frames, elapsed, settle;

        void Start()
        {
            if (!XRSettings.isDeviceActive) { enabled = false; return; }
            float hz = OVRManager.display != null ? OVRManager.display.displayFrequency : 0f;
            if (hz >= 60f) targetFps = hz;
            Apply(startScale);
            OVRManager.foveatedRenderingLevel = OVRManager.FoveatedRenderingLevel.Low;
            OVRManager.useDynamicFoveatedRendering = true;
        }

        void Apply(float scale)
        {
            Scale = scale;
            XRSettings.eyeTextureResolutionScale = scale;
            settle = 2f; // the switch itself hitches, so don't count the next couple of seconds
            frames = elapsed = 0;
            Debug.Log($"[DJMaster] Eye resolution {scale:0.0}x (target {targetFps:0} fps)");
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (settle > 0) { settle -= dt; return; }
            frames++;
            elapsed += dt;
            if (elapsed < window) return;
            float next = NextScale(Scale, frames / elapsed, targetFps, minScale, step, tolerance);
            frames = elapsed = 0;
            if (next < Scale) Apply(next);
        }

        // Steps down one notch when the average is under the target, otherwise stays. Pure, so it's unit tested.
        public static float NextScale(float scale, float avgFps, float target, float min = 1f, float step = 0.1f, float tolerance = 0.95f)
        {
            if (avgFps >= target * tolerance || scale <= min + 1e-4f) return scale;
            return Mathf.Max(min, Mathf.Round((scale - step) * 100f) / 100f);
        }
    }
}
