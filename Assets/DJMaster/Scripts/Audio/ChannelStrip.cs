using UnityEngine;

namespace DJMaster
{
    // One mixer channel: GAIN (trim), 3-band isolator EQ, a sweep filter (left = low-pass, right = high-pass), gain, and a
    // beat-synced ECHO. The echo sits after the fader, so pulling the fader down lets the echoes ring out ("echo out").
    // Sits on the deck's AudioSource and processes its output on the audio thread.
    public class ChannelStrip : MonoBehaviour
    {
        [Range(0, 1)] public float trim = 0.5f; // the GAIN knob: 0.5 = 0 dB, see DJMath.TrimGain
        [Range(0, 1)] public float low = 0.5f;
        [Range(0, 1)] public float mid = 0.5f;
        [Range(0, 1)] public float high = 0.5f;
        [Range(0, 1)] public float filter = 0.5f;
        public float gain;          // final linear gain, written by Mixer
        public bool echo;
        public float echoSeconds = 0.35f; // set by the deck: three quarters of a beat

        public float Level { get; private set; } // smoothed output peak, for meters

        // The channel meter reads before the fader, like on a real mixer: it shows what GAIN and the EQ do to the song,
        // so you can set the level while the song is only in your headphones. 1 = full scale, the top of the meter.
        public float PreLevel { get; private set; }
        // The loudest peak of the last second or so. Peaks jump around, so the lesson and the crowd go by this.
        public float PeakHold { get; private set; }
        // Seconds the channel has been hitting full scale (in the red) without a break. A clipped signal distorts.
        public float ClipTime { get; private set; }

        const double LowCross = 250, HighCross = 3200;
        readonly Biquad[] lowBand = { new(), new() };
        readonly Biquad[] highBand = { new(), new() };
        readonly Biquad[] filt = { new(), new() };
        int sampleRate;
        float lastGain, lastTrim = -1, lastL = 1, lastM = 1, lastH = 1, lastFilter = -1;
        volatile float peak, prePeak;
        float holdTimer, sinceClip = 10f;
        float[] echoBuffer; // interleaved stereo, 2 seconds
        int echoPos;

        void Awake()
        {
            sampleRate = AudioSettings.outputSampleRate;
            echoBuffer = new float[sampleRate * 2 * 2];
            for (int c = 0; c < 2; c++)
            {
                lowBand[c].SetLowpass(sampleRate, LowCross, 0.707);
                highBand[c].SetHighpass(sampleRate, HighCross, 0.707);
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            float p = peak;
            Level = Mathf.Max(p, Level - dt * 1.5f);
            peak = 0;

            float pre = prePeak;
            prePeak = 0;
            PreLevel = Mathf.Max(pre, PreLevel - dt * 1.5f);
            if (pre >= PeakHold) { PeakHold = pre; holdTimer = 1.2f; }
            else if ((holdTimer -= dt) <= 0) PeakHold = Mathf.Max(pre, PeakHold - dt * 0.5f);
            // A short gap between clipped peaks still counts as "in the red", or a loud song would flicker in and out
            sinceClip = pre >= DJMath.ClipLevel ? 0 : sinceClip + dt;
            ClipTime = sinceClip < 0.5f ? ClipTime + dt : 0;
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
            if (sampleRate == 0) return;
            float gL = DJMath.EqGain(low), gM = DJMath.EqGain(mid), gH = DJMath.EqGain(high);
            float g = gain, f = filter, tr = DJMath.TrimGain(trim);
            if (lastTrim < 0) lastTrim = tr; // no ramp up from 0 dB on the first buffer, or a turned-down GAIN would spike the meter
            bool echoIn = echo;
            int echoFrames = echoBuffer.Length / 2;
            int delay = Mathf.Clamp((int)(echoSeconds * sampleRate), 1, echoFrames - 1);

            bool filterOn = Mathf.Abs(f - 0.5f) > 0.03f;
            if (filterOn && Mathf.Abs(f - lastFilter) > 0.002f)
            {
                lastFilter = f;
                for (int c = 0; c < 2; c++)
                {
                    if (f < 0.5f) filt[c].SetLowpass(sampleRate, 20000 * Mathf.Pow(150f / 20000f, (0.5f - f) * 2f), 1.4);
                    else filt[c].SetHighpass(sampleRate, 20 * Mathf.Pow(6000f / 20f, (f - 0.5f) * 2f), 1.4);
                }
            }
            else if (!filterOn) lastFilter = -1;

            int frames = data.Length / channels;
            float inv = frames > 0 ? 1f / frames : 0;
            float pk = 0, pre = 0;
            for (int i = 0; i < frames; i++)
            {
                float u = i * inv; // ramp parameters across the buffer to avoid zipper noise
                float gi = lastGain + (g - lastGain) * u;
                float ti = lastTrim + (tr - lastTrim) * u;
                float li = lastL + (gL - lastL) * u, mi = lastM + (gM - lastM) * u, hi = lastH + (gH - lastH) * u;
                for (int c = 0; c < channels; c++)
                {
                    int k = i * channels + c;
                    int ch = c < 2 ? c : 1;
                    float x = data[k] * ti;
                    // Past full scale the signal clips, the way a real channel in the red distorts
                    if (x > 1f) x = 1f; else if (x < -1f) x = -1f;
                    float lo = lowBand[ch].Process(x);
                    float h = highBand[ch].Process(x);
                    float y = lo * li + (x - lo - h) * mi + h * hi;
                    if (filterOn) y = filt[ch].Process(y);
                    float ay = y < 0 ? -y : y;
                    if (ay > pre) pre = ay;
                    y *= gi;
                    if (ch == c) // the echo runs on the first two channels
                    {
                        int read = ((echoPos - delay + echoFrames) % echoFrames) * 2 + ch;
                        float wet = echoBuffer[read];
                        echoBuffer[echoPos * 2 + ch] = (echoIn ? y * 0.7f : 0f) + wet * 0.55f;
                        y += wet;
                    }
                    data[k] = y;
                    float a = y < 0 ? -y : y;
                    if (a > pk) pk = a;
                }
                echoPos = (echoPos + 1) % echoFrames;
            }
            lastGain = g; lastTrim = tr; lastL = gL; lastM = gM; lastH = gH;
            if (pk > peak) peak = pk;
            if (pre > prePeak) prePeak = pre;
        }
    }
}
