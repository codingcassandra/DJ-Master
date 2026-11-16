using UnityEngine;

namespace DJMaster
{
    // Makes the board sit on your real desk in MY ROOM (#36): a soft contact shadow under it and a faint glow from its
    // LEDs on the desk around it, pulsing a little with the music. Both are flat quads under the board, so they follow it
    // on the desk or floating. The venues have their own floors and lighting, so it's MY ROOM only
    public class BoardGrounding : MonoBehaviour
    {
        public EnvironmentManager environment;
        public Renderer shadow, glow;
        [Tooltip("How much the glow covers when the room is quiet, and how much the beat adds.")]
        public float glowBase = 0.07f, glowBeat = 0.06f;

        MaterialPropertyBlock block;

        public bool Showing => environment != null && environment.Mode == EnvMode.Room && !environment.MenuScreen;

        void LateUpdate()
        {
            bool on = Showing;
            if (shadow != null) shadow.enabled = on;
            if (glow == null) return;
            glow.enabled = on;
            if (!on) return;
            block ??= new MaterialPropertyBlock();
            float a = glowBase + (BeatBus.Playing ? glowBeat * (0.4f * BeatBus.Loudness + 0.6f * BeatBus.Pulse) : 0f);
            block.SetFloat("_Alpha", a);
            glow.SetPropertyBlock(block);
        }
    }
}
