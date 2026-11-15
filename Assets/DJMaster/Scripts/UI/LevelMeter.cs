using UnityEngine;

namespace DJMaster
{
    // A channel's level meter on the mixer: a column of lights, green at the bottom, then yellow, and red at the top.
    // It reads the channel before the fader (ChannelStrip.PreLevel), the way real mixers do, so you can set GAIN on a
    // song that's only in your headphones.
    public class LevelMeter : MonoBehaviour
    {
        public ChannelStrip strip;
        public Renderer[] lights = new Renderer[8]; // bottom to top

        static readonly Color Off = new Color(0.06f, 0.06f, 0.07f);
        MaterialPropertyBlock block;
        int lastLit = -1;
        bool lastSafe;

        // Okabe-Ito colors in color-blind mode, so green and red don't depend on red-green vision. The position
        // (higher = louder, the top light alone = clipping) carries the meaning too.
        public static Color LightColor(int i)
        {
            bool safe = BoardColors.ColorSafe;
            if (i >= DJMath.MeterGreen + DJMath.MeterYellow) return safe ? new Color32(0xD5, 0x5E, 0x00, 0xFF) : new Color(1f, 0.12f, 0.1f);
            if (i >= DJMath.MeterGreen) return safe ? new Color32(0xF0, 0xE4, 0x42, 0xFF) : new Color(1f, 0.85f, 0.1f);
            return safe ? new Color32(0x56, 0xB4, 0xE9, 0xFF) : new Color(0.15f, 1f, 0.35f);
        }

        void Update()
        {
            if (strip == null) return;
            int lit = DJMath.MeterLights(strip.PreLevel);
            if (lit == lastLit && lastSafe == BoardColors.ColorSafe) return;
            lastLit = lit; lastSafe = BoardColors.ColorSafe;
            block ??= new MaterialPropertyBlock();
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] == null) continue;
                block.SetColor("_Color", i < lit ? LightColor(i) : Off);
                lights[i].SetPropertyBlock(block);
            }
        }
    }
}
