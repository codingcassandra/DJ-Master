using System;

namespace DJMaster
{
    public enum Genre { House, Techno, Disco, Electro, EDM, Synthwave }

    [Serializable]
    public struct TrackInfo
    {
        public string title;
        public string artist;
        public float bpm;
        public int root;   // pitch class, 0 = C
        public Genre genre;
        public int seed;
        // A real recording instead of a synthesized loop: Resources/Music/<clip>, a 16-bar excerpt that starts on beat 1.
        public string clip;
        public string license;

        public TrackInfo(string title, string artist, float bpm, int root, Genre genre, int seed)
        {
            this.title = title; this.artist = artist; this.bpm = bpm;
            this.root = root; this.genre = genre; this.seed = seed;
            clip = null; license = null;
        }

        public static TrackInfo Recording(string title, string artist, float bpm, Genre genre, string clip, string license = "CC0") =>
            new(title, artist, bpm, 0, genre, 0) { clip = clip, license = license };

        public bool IsRecording => !string.IsNullOrEmpty(clip);

        // Synthesized tracks know their key; for recordings it isn't shown.
        public string Key => IsRecording ? "" : KeyNames[((root % 12) + 12) % 12] + "m";
        static readonly string[] KeyNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
    }

    // Renders a seamless 16-bar dance loop from scratch, so the game ships with no licensed music.
    // The loop is arranged in DJ-friendly phrases (bar numbers are 1-based, as shown in the game):
    //   bars 1-8   DROP       full groove: kick, clap, hats, bass, chords
    //   bars 9-12  GROOVE     kick, hats, bass and a lead arp
    //   bars 13-14 BREAKDOWN  no kick, no bass: pads, lead, riser
    //   bars 15-16 BUILD      kick returns, snare roll into the next drop
    // Thread safe: pure managed code, no Unity API, so it runs on a worker thread.
    public static class TrackSynth
    {
        public const int SampleRate = 44100;
        public const int Bars = 16;
        public const int BeatsPerBar = 4;
        public const int TotalBeats = Bars * BeatsPerBar;

        public static int FrameCount(float bpm) => (int)Math.Round(TotalBeats * 60.0 / bpm * SampleRate);

        public enum Section { Drop, Groove, Breakdown, Build }

        public static Section SectionOfBar(int bar) => bar < 8 ? Section.Drop : bar < 12 ? Section.Groove : bar < 14 ? Section.Breakdown : Section.Build;

        // Minor-key loop: i - VI - III - VII, one chord per bar.
        static readonly int[][] Chords = { new[] { 0, 3, 7 }, new[] { 8, 12, 15 }, new[] { 3, 7, 10 }, new[] { 10, 14, 17 } };

        sealed class Buffers
        {
            public readonly float[] drumL, drumR, musicL, musicR, sendL, sendR, duck;
            public readonly int frames;
            public Buffers(int n)
            {
                frames = n;
                drumL = new float[n]; drumR = new float[n];
                musicL = new float[n]; musicR = new float[n];
                sendL = new float[n]; sendR = new float[n];
                duck = new float[n];
            }
        }

        // Returns interleaved stereo samples.
        public static float[] Render(TrackInfo t)
        {
            int frames = FrameCount(t.bpm);
            var b = new Buffers(frames);
            var rng = new Random(t.seed);
            double step = 60.0 / t.bpm / 4.0 * SampleRate; // one 16th note, in samples
            int bassRoot = 33 + ((t.root % 12) + 12) % 12; // A1..G#2
            if (bassRoot > 40) bassRoot -= 12;

            bool[] clapAlt = new bool[16];
            for (int i = 0; i < 16; i++) clapAlt[i] = rng.NextDouble() < 0.12;
            int arpShape = rng.Next(3);
            bool chordOnOffbeat = rng.NextDouble() < 0.5;

            for (int i = 0; i < frames; i++) b.duck[i] = 1f;

            for (int bar = 0; bar < Bars; bar++)
            {
                var sec = SectionOfBar(bar);
                int[] chord = Chords[bar % 4];
                bool drums = sec != Section.Breakdown;
                bool bass = sec == Section.Drop || sec == Section.Groove;

                for (int s = 0; s < 16; s++)
                {
                    int at = (int)Math.Round((bar * 16 + s) * step);
                    bool quarter = s % 4 == 0;
                    bool offbeat = s % 4 == 2;

                    // Kick: four on the floor. In the last build bar it doubles up to 8ths.
                    if (drums && (quarter || (bar == 15 && s % 2 == 0)))
                    {
                        Kick(b, at, bar == 15 ? 0.8f : 1f);
                        Duck(b, at, step * 4);
                    }

                    if (sec == Section.Build)
                    {
                        float vel = (bar - 14) * 0.5f + s / 32f;
                        bool roll = bar == 15 ? true : s % 2 == 0;
                        if (roll) Snare(b, at, 0.15f + vel * 0.35f, rng);
                    }

                    if (sec == Section.Drop || sec == Section.Groove)
                    {
                        bool backbeat = s == 4 || s == 12 || (clapAlt[s] && s % 2 == 1 && bar % 4 == 3);
                        if (backbeat)
                        {
                            if (t.genre == Genre.Disco) Snare(b, at, 0.55f, rng);
                            else Clap(b, at, t.genre == Genre.Techno ? 0.35f : 0.5f, rng);
                        }
                    }

                    if (drums)
                    {
                        switch (t.genre)
                        {
                            case Genre.House:
                                if (offbeat) OpenHat(b, at, 0.55f, rng);
                                else if (s % 2 == 1) Hat(b, at, 0.25f, rng);
                                break;
                            case Genre.Techno:
                                Hat(b, at, s % 4 == 2 ? 0.45f : 0.22f, rng);
                                if (offbeat) Ride(b, at, 0.4f, rng);
                                break;
                            case Genre.Disco:
                                if (s % 2 == 0 && !offbeat) Hat(b, at, 0.35f, rng);
                                if (offbeat) OpenHat(b, at, 0.5f, rng);
                                break;
                        }
                    }

                    if (bass)
                    {
                        int note = bassRoot + chord[0];
                        if (note > bassRoot + 7) note -= 12;
                        switch (t.genre)
                        {
                            case Genre.House:
                                if (offbeat) Bass(b, at, note, step * 1.6, 1f);
                                else if (s == 15 && bar % 2 == 1) Bass(b, at, note + 12, step * 0.8, 0.7f);
                                break;
                            case Genre.Techno:
                                if (!quarter) Bass(b, at, note, step * 0.85, s % 4 == 2 ? 0.95f : 0.7f);
                                break;
                            case Genre.Disco:
                                if (s % 2 == 0) Bass(b, at, note + (s % 4 == 2 ? 12 : 0), step * 1.5, 0.95f);
                                break;
                        }
                    }

                    if (sec == Section.Drop)
                    {
                        bool stab = t.genre switch
                        {
                            Genre.Disco => offbeat,
                            Genre.Techno => s == 6 || s == 14,
                            _ => chordOnOffbeat ? (s == 3 || s == 10) : (s == 6 || s == 14),
                        };
                        if (stab) ChordStab(b, at, chord, t.root, step * (t.genre == Genre.Disco ? 1.2 : 1.8), t.genre == Genre.Disco);
                    }

                    if ((sec == Section.Groove || sec == Section.Breakdown) && (s % 2 == 0 || t.genre == Genre.Techno))
                    {
                        int idx = arpShape switch
                        {
                            0 => (s / 2) % 3,
                            1 => new[] { 0, 1, 2, 1 }[(s / 2) % 4],
                            _ => new[] { 2, 0, 1, 0, 2, 1 }[(s / 2) % 6],
                        };
                        int octave = (s / 8) % 2 == 1 && sec == Section.Breakdown ? 12 : 0;
                        Lead(b, at, 60 + ((t.root % 12) + 12) % 12 + chord[idx] + octave, step * 1.5, sec == Section.Breakdown ? 0.9f : 0.6f);
                    }
                }

                if (sec == Section.Breakdown || sec == Section.Build)
                    Pad(b, (int)Math.Round(bar * 16 * step), chord, t.root, step * 16);
            }

            Riser(b, (int)Math.Round(12 * 16 * step), (int)Math.Round(4 * 16 * step), rng);
            PingPong(b, (int)Math.Round(step * 3));
            return Master(b);
        }

        // ---------------- voices ----------------

        static void Add(float[] buf, int i, float v) => buf[i % buf.Length] += v;

        static void Kick(Buffers b, int at, float vel)
        {
            int n = (int)(0.34 * SampleRate);
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                double t = (double)i / SampleRate;
                double f = 46 + 120 * Math.Exp(-t * 32);
                phase += 2 * Math.PI * f / SampleRate;
                double amp = Math.Exp(-t * 6.5) * (t < 0.002 ? t / 0.002 : 1);
                float v = (float)(Math.Sin(phase) * amp * 0.95 * vel);
                Add(b.drumL, at + i, v); Add(b.drumR, at + i, v);
            }
        }

        // Sidechain pump: music ducks under every kick.
        static void Duck(Buffers b, int at, double len)
        {
            int n = (int)len;
            for (int i = 0; i < n; i++)
            {
                double t = (double)i / SampleRate;
                float g = (float)(1 - 0.55 * Math.Exp(-t * 11));
                int k = (at + i) % b.frames;
                if (g < b.duck[k]) b.duck[k] = g;
            }
        }

        static void Clap(Buffers b, int at, float vel, Random rng)
        {
            int n = (int)(0.22 * SampleRate);
            float lpL = 0, lpR = 0, hpL = 0, hpR = 0;
            for (int i = 0; i < n; i++)
            {
                double t = (double)i / SampleRate;
                double env = t < 0.03 ? Math.Exp(-(t % 0.01) * 280) : Math.Exp(-(t - 0.03) * 22);
                float nl = (float)(rng.NextDouble() * 2 - 1), nr = (float)(rng.NextDouble() * 2 - 1);
                lpL += 0.35f * (nl - lpL); lpR += 0.35f * (nr - lpR);
                hpL += 0.08f * (lpL - hpL); hpR += 0.08f * (lpR - hpR);
                Add(b.drumL, at + i, (lpL - hpL) * (float)env * vel * 0.9f);
                Add(b.drumR, at + i, (lpR - hpR) * (float)env * vel * 0.9f);
            }
        }

        static void Snare(Buffers b, int at, float vel, Random rng)
        {
            int n = (int)(0.16 * SampleRate);
            float hp = 0;
            for (int i = 0; i < n; i++)
            {
                double t = (double)i / SampleRate;
                float noise = (float)(rng.NextDouble() * 2 - 1);
                hp += 0.15f * (noise - hp);
                float v = (float)(Math.Sin(2 * Math.PI * 190 * t) * Math.Exp(-t * 25) * 0.4 + (noise - hp) * Math.Exp(-t * 20) * 0.6) * vel;
                Add(b.drumL, at + i, v); Add(b.drumR, at + i, v);
            }
        }

        static void Hat(Buffers b, int at, float vel, Random rng) => Noise(b, at, 0.05, 70, vel * 0.32f, 0.25f, rng);
        static void OpenHat(Buffers b, int at, float vel, Random rng) => Noise(b, at, 0.24, 13, vel * 0.22f, -0.2f, rng);
        static void Ride(Buffers b, int at, float vel, Random rng) => Noise(b, at, 0.35, 7, vel * 0.1f, 0.35f, rng);

        static void Noise(Buffers b, int at, double dur, double decay, float vel, float pan, Random rng)
        {
            int n = (int)(dur * SampleRate);
            float lp = 0, prev = 0;
            float gl = vel * (1 - Math.Max(0, pan)), gr = vel * (1 + Math.Min(0, pan));
            for (int i = 0; i < n; i++)
            {
                double t = (double)i / SampleRate;
                float x = (float)(rng.NextDouble() * 2 - 1);
                lp += 0.6f * (x - lp);
                float hp = lp - prev; prev = lp; // crude high-pass for a bright metallic tick
                float v = hp * (float)Math.Exp(-t * decay);
                Add(b.drumL, at + i, v * gl); Add(b.drumR, at + i, v * gr);
            }
        }

        static double Hz(int midi) => 440.0 * Math.Pow(2, (midi - 69) / 12.0);

        static void Bass(Buffers b, int at, int midi, double len, float vel)
        {
            int n = (int)len + (int)(0.01 * SampleRate);
            double f = Hz(midi);
            double p1 = 0, p2 = 0.3;
            float y1 = 0, y2 = 0;
            for (int i = 0; i < n; i++)
            {
                double t = (double)i / SampleRate;
                p1 += f / SampleRate; p2 += f * 1.006 / SampleRate;
                float saw = (float)((p1 % 1) * 2 - 1 + (p2 % 1) * 2 - 1) * 0.5f;
                float sub = (float)Math.Sin(2 * Math.PI * p1);
                double cutoff = 180 + 1400 * Math.Exp(-t * 16);
                float a = (float)(1 - Math.Exp(-2 * Math.PI * cutoff / SampleRate));
                y1 += a * (saw - y1); y2 += a * (y1 - y2);
                double env = Math.Min(1, t / 0.003) * (i > len ? Math.Max(0, 1 - (i - len) / (0.01 * SampleRate)) : 1);
                float v = (y2 * 0.7f + sub * 0.5f) * (float)env * vel * 0.42f;
                Add(b.musicL, at + i, v); Add(b.musicR, at + i, v);
            }
        }

        static void ChordStab(Buffers b, int at, int[] chord, int root, double len, bool bright)
        {
            foreach (int iv in chord)
                Saws(b, at, 48 + ((root % 12) + 12) % 12 + iv, len, 0.0, bright ? 3200 : 1700, 9, 0.075f, 0.35f);
        }

        static void Pad(Buffers b, int at, int[] chord, int root, double len)
        {
            foreach (int iv in chord)
                Saws(b, at, 48 + ((root % 12) + 12) % 12 + iv, len, 0.25, 1100, 0.6, 0.05f, 0.15f);
        }

        static void Saws(Buffers b, int at, int midi, double len, double attack, double cutoff, double decay, float vel, float send)
        {
            int n = (int)len;
            double f = Hz(midi);
            double pl = 0, pr = 0.5;
            float l1 = 0, r1 = 0;
            for (int i = 0; i < n; i++)
            {
                double t = (double)i / SampleRate;
                pl += f * 0.996 / SampleRate; pr += f * 1.004 / SampleRate;
                float sl = (float)((pl % 1) * 2 - 1), sr = (float)((pr % 1) * 2 - 1);
                float a = (float)(1 - Math.Exp(-2 * Math.PI * cutoff * (0.35 + 0.65 * Math.Exp(-t * decay)) / SampleRate));
                l1 += a * (sl - l1); r1 += a * (sr - r1);
                double env = (attack > 0 ? Math.Min(1, t / attack) : Math.Min(1, t / 0.004)) * Math.Min(1, (n - i) / (0.02 * SampleRate));
                float g = (float)env * vel;
                Add(b.musicL, at + i, l1 * g); Add(b.musicR, at + i, r1 * g);
                Add(b.sendL, at + i, l1 * g * send); Add(b.sendR, at + i, r1 * g * send);
            }
        }

        static void Lead(Buffers b, int at, int midi, double len, float vel)
        {
            int n = (int)len;
            double f = Hz(midi);
            double p = 0;
            float lp = 0;
            for (int i = 0; i < n; i++)
            {
                double t = (double)i / SampleRate;
                p += f / SampleRate;
                float sq = (p % 1) < 0.5 ? 1f : -1f;
                float s = (float)Math.Sin(2 * Math.PI * p);
                lp += 0.25f * (sq * 0.5f + s * 0.5f - lp);
                float v = lp * (float)(Math.Exp(-t * 9) * Math.Min(1, t / 0.002)) * vel * 0.12f;
                Add(b.musicL, at + i, v * 0.9f); Add(b.musicR, at + i, v * 0.7f);
                Add(b.sendL, at + i, v * 0.5f); Add(b.sendR, at + i, v * 0.5f);
            }
        }

        static void Riser(Buffers b, int at, int len, Random rng)
        {
            float l = 0, r = 0;
            for (int i = 0; i < len; i++)
            {
                double u = (double)i / len;
                double cutoff = 200 * Math.Pow(40, u);
                float a = (float)(1 - Math.Exp(-2 * Math.PI * cutoff / SampleRate));
                l += a * ((float)(rng.NextDouble() * 2 - 1) - l);
                r += a * ((float)(rng.NextDouble() * 2 - 1) - r);
                float g = (float)(u * u * 0.22);
                Add(b.musicL, at + i, l * g); Add(b.musicR, at + i, r * g);
            }
        }

        // Ping-pong delay on the send bus. Runs the line twice round the loop so the tail wraps seamlessly.
        static void PingPong(Buffers b, int delay)
        {
            int n = b.frames;
            var outL = new float[n];
            var outR = new float[n];
            float[] lineL = new float[delay], lineR = new float[delay];
            int w = 0;
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < n; i++)
                {
                    float dl = lineL[w], dr = lineR[w];
                    lineL[w] = b.sendR[i] + dr * 0.38f; // cross-feed for the ping-pong
                    lineR[w] = b.sendL[i] * 0.2f + dl * 0.38f;
                    w = (w + 1) % delay;
                    if (pass == 1) { outL[i] = dl; outR[i] = dr; }
                }
            }
            for (int i = 0; i < n; i++) { b.musicL[i] += outL[i] * 0.6f; b.musicR[i] += outR[i] * 0.6f; }
        }

        static float[] Master(Buffers b)
        {
            int n = b.frames;
            var outBuf = new float[n * 2];
            float peak = 0.0001f;
            for (int i = 0; i < n; i++)
            {
                float l = b.drumL[i] + b.musicL[i] * b.duck[i];
                float r = b.drumR[i] + b.musicR[i] * b.duck[i];
                l = (float)Math.Tanh(l * 1.15); r = (float)Math.Tanh(r * 1.15);
                outBuf[i * 2] = l; outBuf[i * 2 + 1] = r;
                peak = Math.Max(peak, Math.Max(Math.Abs(l), Math.Abs(r)));
            }
            float norm = 0.85f / peak;
            for (int i = 0; i < outBuf.Length; i++) outBuf[i] *= norm;
            return outBuf;
        }
    }
}
