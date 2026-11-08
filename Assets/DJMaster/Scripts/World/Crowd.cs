using UnityEngine;
using UnityEngine.Rendering;

namespace DJMaster
{
    // A dance floor of low-poly people drawn with GPU instancing. Each person has their own build, skin tone,
    // hair and outfit. They bend their knees on the beat, nod, sway, and their arms follow the crowd's energy:
    // hanging loose, grooving at chest height, hands up, fist pumps, phones and glow sticks in the air.
    public class Crowd : MonoBehaviour
    {
        public int count = 120;
        public Vector2 areaMin = new Vector2(-6, 2);   // local x/z rectangle
        public Vector2 areaMax = new Vector2(6, 13);
        public float floorY;
        [Tooltip("Lit material using DJMaster/CrowdInstanced, so every body part can have its own color.")]
        public Material bodyMaterial;
        public Material glowMaterial;                  // glow sticks / phone lights
        [Range(0, 1)] public float glowChance = 0.15f;
        public int seed = 1;
        [Tooltip("People further than this from the DJ skip small details (hands, shoes, long hair).")]
        public float detailDistance = 16f;

        enum Part { Head, Hair, LongHair, Neck, Torso, Pelvis, UpperArm, Forearm, Hand, Thigh, Shin, Shoe, Stick, Count }

        struct Person
        {
            public Vector3 pos;
            public float yaw, height, phase, hype, bulk, shoulder, hip, stance, armStyle;
            public bool glow, longHair, bald, shorts, longSleeves;
            public Vector4 skin, hair, top, pants, shoes;
        }

        Person[] people;
        Mesh[] meshes;
        Matrix4x4[][] matrices;
        Vector4[][] colors;
        int[] counts;
        readonly Matrix4x4[] batchM = new Matrix4x4[1023];
        readonly Vector4[] batchC = new Vector4[1023];
        MaterialPropertyBlock block;
        static readonly int ColorId = Shader.PropertyToID("_Color");

        static readonly Color[] Skins =
        {
            new(0.97f, 0.82f, 0.70f), new(0.92f, 0.72f, 0.56f), new(0.82f, 0.60f, 0.43f),
            new(0.65f, 0.45f, 0.30f), new(0.47f, 0.32f, 0.21f), new(0.32f, 0.21f, 0.14f),
        };
        static readonly Color[] Hairs =
        {
            new(0.04f, 0.03f, 0.03f), new(0.04f, 0.03f, 0.03f), new(0.14f, 0.08f, 0.05f), new(0.14f, 0.08f, 0.05f),
            new(0.32f, 0.2f, 0.1f), new(0.78f, 0.62f, 0.36f), new(0.5f, 0.18f, 0.08f), new(0.9f, 0.88f, 0.82f),
            new(1f, 0.35f, 0.7f), new(0.25f, 0.45f, 1f),
        };
        static readonly Color[] Tops =
        {
            new(0.06f, 0.06f, 0.07f), new(0.06f, 0.06f, 0.07f), new(0.92f, 0.92f, 0.9f), new(0.75f, 0.12f, 0.15f),
            new(0.12f, 0.18f, 0.42f), new(0.95f, 0.75f, 0.2f), new(0.3f, 0.65f, 0.4f), new(0.55f, 0.3f, 0.75f),
            new(1f, 0.45f, 0.65f), new(0.45f, 0.45f, 0.48f), new(0.2f, 0.95f, 0.7f), new(1f, 0.5f, 0.15f),
        };
        static readonly Color[] Pants =
        {
            new(0.17f, 0.24f, 0.4f), new(0.12f, 0.16f, 0.28f), new(0.05f, 0.05f, 0.06f), new(0.05f, 0.05f, 0.06f),
            new(0.55f, 0.48f, 0.35f), new(0.35f, 0.35f, 0.37f), new(0.85f, 0.85f, 0.82f),
        };
        static readonly Color[] ShoeColors = { new(0.95f, 0.95f, 0.93f), new(0.06f, 0.06f, 0.06f), new(0.06f, 0.06f, 0.06f), new(0.6f, 0.15f, 0.12f) };

        void Awake() => Init();

        public void Init()
        {
            if (people != null) return;
            meshes = new Mesh[(int)Part.Count];
            meshes[(int)Part.Head] = MeshKit.Sphere(12, 8);
            meshes[(int)Part.Hair] = meshes[(int)Part.Head];
            meshes[(int)Part.LongHair] = MeshKit.Prism(8, 0.085f, 0.06f, 1f);
            meshes[(int)Part.Neck] = MeshKit.Prism(6, 0.05f, 0.045f, 1f);
            meshes[(int)Part.Torso] = MeshKit.Prism(10, 0.16f, 0.2f, 1f);   // hips -> shoulders, x/z scaled per person
            meshes[(int)Part.Pelvis] = MeshKit.Prism(10, 0.17f, 0.16f, 1f);
            meshes[(int)Part.UpperArm] = MeshKit.Prism(7, 0.047f, 0.04f, 1f);
            meshes[(int)Part.Forearm] = MeshKit.Prism(7, 0.039f, 0.03f, 1f);
            meshes[(int)Part.Hand] = meshes[(int)Part.Head];
            meshes[(int)Part.Thigh] = MeshKit.Prism(8, 0.075f, 0.055f, 1f);
            meshes[(int)Part.Shin] = MeshKit.Prism(8, 0.053f, 0.04f, 1f);
            meshes[(int)Part.Shoe] = MeshKit.Prism(4, 0.707f, 0.707f, 1f);  // a unit box once turned 45 degrees
            meshes[(int)Part.Stick] = MeshKit.Prism(4, 0.015f, 0.015f, 1f);

            var rng = new System.Random(seed);
            float R() => (float)rng.NextDouble();
            Vector4 Pick(Color[] palette, float vary)
            {
                var c = palette[rng.Next(palette.Length)] * (1f - vary * 0.5f + R() * vary);
                return (Vector4)(QualitySettings.activeColorSpace == ColorSpace.Linear ? c.linear : c);
            }

            people = new Person[count];
            int perPerson = (int)Part.Count;
            matrices = new Matrix4x4[perPerson][];
            colors = new Vector4[perPerson][];
            counts = new int[perPerson];
            for (int k = 0; k < perPerson; k++) { matrices[k] = new Matrix4x4[count * 2]; colors[k] = new Vector4[count * 2]; }

            for (int i = 0; i < count; i++)
            {
                float x = Mathf.Lerp(areaMin.x, areaMax.x, R());
                float z = Mathf.Lerp(areaMin.y, areaMax.y, R());
                float height = 1.55f + R() * 0.38f;
                float broad = R();
                people[i] = new Person
                {
                    pos = new Vector3(x, floorY, z),
                    // Most people face the DJ; a few turn toward their friends.
                    yaw = 180f + (R() < 0.15f ? (R() - 0.5f) * 140f : (R() - 0.5f) * 35f),
                    height = height,
                    phase = R(),
                    hype = R(),
                    bulk = 0.85f + R() * 0.35f,
                    shoulder = Mathf.Lerp(0.16f, 0.22f, broad) * height / 1.75f,
                    hip = Mathf.Lerp(0.15f, 0.11f, broad) * height / 1.75f,
                    stance = 0.1f + R() * 0.08f,
                    armStyle = R(),
                    glow = R() < glowChance,
                    longHair = R() < 0.4f,
                    bald = R() < 0.06f,
                    shorts = R() < 0.15f,
                    longSleeves = R() < 0.35f,
                    skin = Pick(Skins, 0.08f),
                    hair = Pick(Hairs, 0.1f),
                    top = Pick(Tops, 0.15f),
                    pants = Pick(Pants, 0.12f),
                    shoes = Pick(ShoeColors, 0.05f),
                };
            }
        }

        void Update()
        {
            float energy = BeatBus.Energy * Mathf.Clamp01(BeatBus.Loudness * 2f);
            Pose(Time.time, BeatBus.Playing ? BeatBus.Beat : Time.time * 2.0, BeatBus.Playing, energy);
            Draw(null);
        }

        // Places every body part for this moment. Public so the editor can snapshot the crowd mid-dance.
        public void Pose(float time, double beat, bool playing, float energy)
        {
            Init();
            System.Array.Clear(counts, 0, counts.Length);
            var root = transform.localToWorldMatrix;
            for (int i = 0; i < people.Length; i++) PosePerson(ref people[i], root, time, beat, playing, energy);
        }

        void Add(Part part, Matrix4x4 m, Vector4 color)
        {
            int k = (int)part;
            if (counts[k] >= matrices[k].Length) return;
            matrices[k][counts[k]] = m;
            colors[k][counts[k]] = color;
            counts[k]++;
        }

        // A limb segment from a to b. Meshes grow along +Y with unit length.
        void Limb(Part part, Matrix4x4 body, Vector3 a, Vector3 b, float thickness, Vector4 color)
        {
            Vector3 d = b - a;
            float len = d.magnitude;
            if (len < 1e-4f) return;
            Add(part, body * Matrix4x4.TRS(a, Quaternion.FromToRotation(Vector3.up, d / len), new Vector3(thickness, len, thickness)), color);
        }

        // Two-bone IK: where the elbow/knee goes so a limb of lengths l1 and l2 reaches from root toward target,
        // bending toward hint. Also pulls the target in if it's out of reach.
        static Vector3 Joint(Vector3 root, ref Vector3 target, float l1, float l2, Vector3 hint)
        {
            Vector3 d = target - root;
            float dist = Mathf.Clamp(d.magnitude, Mathf.Abs(l1 - l2) + 0.01f, l1 + l2 - 0.002f);
            Vector3 dir = d.sqrMagnitude > 1e-8f ? d.normalized : Vector3.down;
            target = root + dir * dist;
            float along = (l1 * l1 - l2 * l2 + dist * dist) / (2 * dist);
            float h = Mathf.Sqrt(Mathf.Max(0, l1 * l1 - along * along));
            Vector3 perp = hint - dir * Vector3.Dot(hint, dir);
            perp = perp.sqrMagnitude > 1e-6f ? perp.normalized : Vector3.forward;
            return root + dir * along + perp * h;
        }

        void PosePerson(ref Person p, Matrix4x4 root, float time, double beat, bool playing, float energy)
        {
            var body = root * Matrix4x4.TRS(p.pos, Quaternion.Euler(0, p.yaw, 0), Vector3.one);
            bool near = p.pos.magnitude < detailDistance;
            float H = p.height;

            // ---- rhythm ----
            bool dancing = playing && p.hype < 0.3f + energy;
            float b = (float)((beat + p.phase * 0.1) % 1.0);
            float onBeat = 0.5f + 0.5f * Mathf.Cos(b * Mathf.PI * 2);              // 1 on the beat, 0 on the off-beat
            float idle = 0.5f + 0.5f * Mathf.Sin(time * 1.1f + p.phase * 6.28f);
            float dip = dancing ? onBeat * (0.025f + 0.06f * energy) : idle * 0.01f;
            bool jumper = dancing && energy > 0.65f && p.hype < (energy - 0.55f) * 1.5f;
            float jump = jumper ? Mathf.Pow(1f - b, 3f) * energy * energy * 0.22f : 0f;
            float swayPhase = (float)(beat * 0.5) * Mathf.PI * 2 + p.phase * 6.28f;
            float sway = Mathf.Sin(swayPhase) * (dancing ? 0.035f + 0.02f * energy : 0.012f);

            // ---- legs (feet planted, knees take the bounce) ----
            float thigh = 0.245f * H, shin = 0.235f * H, footH = 0.06f * H / 1.75f;
            float legReach = thigh + shin;
            Vector3 hipC = new Vector3(sway, footH + legReach * 0.985f - dip + jump, 0);
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 hipJ = hipC + new Vector3(p.hip * 0.8f * s, 0, 0);
                Vector3 foot = new Vector3(p.stance * s, footH + jump, (s * 0.03f + p.phase * 0.04f) - 0.01f);
                Vector3 knee = Joint(hipJ, ref foot, thigh, shin, Vector3.forward + Vector3.right * 0.15f * s);
                Limb(Part.Thigh, body, hipJ, knee, p.bulk, p.pants);
                Limb(Part.Shin, body, knee, foot, p.bulk, p.shorts ? p.skin : p.pants);
                if (near)
                    Add(Part.Shoe, body * Matrix4x4.TRS(new Vector3(foot.x, foot.y - footH, foot.z + 0.05f), Quaternion.identity,
                        new Vector3(0.095f, footH, 0.26f) * (H / 1.75f)) * Matrix4x4.Rotate(Quaternion.Euler(0, 45, 0)), p.shoes);
            }

            // ---- torso ----
            float lean = dancing ? 6f + onBeat * 5f * energy : 3f;
            var torsoRot = Quaternion.Euler(lean, 0, -sway * 120f);
            float torsoLen = 0.3f * H;
            Vector3 waist = hipC + Vector3.up * 0.04f;
            Vector3 neckBase = waist + torsoRot * (Vector3.up * torsoLen);
            Add(Part.Pelvis, body * Matrix4x4.TRS(hipC - Vector3.up * 0.09f, torsoRot,
                new Vector3(p.hip / 0.13f, 0.16f, p.hip / 0.13f * 0.75f)), p.pants);
            Add(Part.Torso, body * Matrix4x4.TRS(waist, torsoRot,
                new Vector3(p.shoulder / 0.2f, torsoLen, 0.6f * p.bulk)), p.top);

            // ---- head ----
            float nod = dancing ? (onBeat - 0.5f) * (10f + 10f * energy) : 0f;
            var headRot = torsoRot * Quaternion.Euler(nod, Mathf.Sin(time * 0.4f + p.phase * 9f) * 12f, 0);
            Vector3 neckTop = neckBase + torsoRot * (Vector3.up * 0.07f);
            Limb(Part.Neck, body, neckBase - torsoRot * (Vector3.up * 0.02f), neckTop + headRot * (Vector3.up * 0.02f), p.bulk, p.skin);
            float hs = H / 1.75f;
            Vector3 headC = neckTop + headRot * new Vector3(0, 0.1f, 0.015f) * hs;
            Add(Part.Head, body * Matrix4x4.TRS(headC, headRot, new Vector3(0.085f, 0.112f, 0.1f) * hs), p.skin);
            if (!p.bald)
            {
                Add(Part.Hair, body * Matrix4x4.TRS(headC + headRot * new Vector3(0, 0.022f, -0.012f) * hs, headRot,
                    new Vector3(0.091f, 0.1f, 0.103f) * hs), p.hair);
                if (p.longHair && near)
                    Limb(Part.LongHair, body, headC + headRot * new Vector3(0, 0.03f, -0.035f) * hs,
                        headC + headRot * new Vector3(0, -0.2f, -0.07f) * hs, hs, p.hair);
            }

            // ---- arms ----
            float upper = 0.17f * H, fore = 0.16f * H;
            float wave = Mathf.Sin((float)beat * Mathf.PI + p.phase * 6f);
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 shoulder = neckBase + torsoRot * new Vector3(p.shoulder * 0.9f * s, -0.04f, 0);
                Vector3 hand;
                Vector3 hint;
                bool handsUp = dancing && p.hype < energy * 0.85f;
                bool holdingLight = p.glow && s == 1 && (handsUp || !dancing || p.armStyle > 0.5f);
                if (holdingLight && !handsUp)
                {
                    hand = shoulder + torsoRot * new Vector3(0.05f, 0.25f + 0.03f * onBeat, 0.3f);    // phone up, filming
                    hint = new Vector3(s * 0.6f, -1f, 0);
                }
                else if (handsUp && p.armStyle < 0.35f)
                {
                    bool pumpArm = s == 1;                                                               // fist pump on the beat
                    hand = pumpArm
                        ? shoulder + new Vector3(0.08f * s, 0.32f + 0.25f * (1f - onBeat), 0.12f)
                        : shoulder + torsoRot * new Vector3(0.12f * s, -0.05f + 0.06f * onBeat, 0.22f);
                    hint = new Vector3(s, -0.6f, -0.2f);
                }
                else if (handsUp)
                {
                    hand = shoulder + new Vector3((0.12f + 0.12f * wave * s) * s, 0.5f + 0.04f * onBeat, 0.05f); // hands in the air, waving
                    hint = new Vector3(s, 0, -0.3f);
                }
                else if (dancing)
                {
                    hand = shoulder + torsoRot * new Vector3(0.1f * s - 0.05f * s * onBeat, -0.15f + 0.07f * onBeat, 0.24f); // grooving at chest height
                    hint = new Vector3(s, -1f, -0.3f);
                }
                else
                {
                    hand = shoulder + torsoRot * new Vector3(0.05f * s, -(upper + fore) * 0.95f, 0.04f + 0.03f * idle); // hanging loose
                    hint = new Vector3(0, 0, -1f);
                }

                Vector3 elbow = Joint(shoulder, ref hand, upper, fore, hint);
                Limb(Part.UpperArm, body, shoulder, elbow, p.bulk, p.top);
                Limb(Part.Forearm, body, elbow, hand, p.bulk, p.longSleeves ? p.top : p.skin);
                if (near) Add(Part.Hand, body * Matrix4x4.TRS(hand + (hand - elbow).normalized * 0.03f, Quaternion.identity, Vector3.one * 0.042f * hs), p.skin);
                if (holdingLight)
                {
                    Vector3 dir = handsUp ? (hand - elbow).normalized : Vector3.up;
                    Limb(Part.Stick, body, hand, hand + dir * (handsUp ? 0.22f : 0.12f), handsUp ? 1f : 2.5f, Vector4.one);
                }
            }
        }

        public void Draw(Camera cam)
        {
            if (bodyMaterial == null || counts == null) return;
            block ??= new MaterialPropertyBlock();
            for (int k = 0; k < (int)Part.Count; k++)
            {
                bool glow = k == (int)Part.Stick;
                var mat = glow ? glowMaterial : bodyMaterial;
                if (mat == null) continue;
                for (int start = 0; start < counts[k]; start += 1023)
                {
                    int n = Mathf.Min(1023, counts[k] - start);
                    System.Array.Copy(matrices[k], start, batchM, 0, n);
                    block.Clear();
                    if (!glow)
                    {
                        System.Array.Copy(colors[k], start, batchC, 0, n);
                        block.SetVectorArray(ColorId, batchC);
                    }
                    Graphics.DrawMeshInstanced(meshes[k], 0, mat, batchM, n, block, ShadowCastingMode.Off, false, gameObject.layer, cam);
                }
            }
        }
    }
}
