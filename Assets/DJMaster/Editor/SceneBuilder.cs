using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace DJMaster.EditorTools
{
    // Builds the whole game from primitives so the project needs no art or audio assets.
    // Menu: DJ Master > Build Scene. Re-run any time; it overwrites the scene.
    //
    // Coordinates: the World root is the DJ's seat on the venue floor, facing +Z.
    // The board's top surface is the Board transform's local y = 0, with +Z away from the player.
    public static class SceneBuilder
    {
        const string GeneratedDir = "Assets/DJMaster/Generated";
        public const string ScenePath = "Assets/Scenes/DJMaster.unity";
        const string SourcePath = "Assets/DJMaster/Editor/SceneBuilder.cs";

        // Changes whenever this file changes, so a scene built by older code can be spotted.
        public static string SourceHash() => File.Exists(SourcePath) ? Hash128.Compute(File.ReadAllText(SourcePath)).ToString() : "";
        const string CorePrefabs = "Packages/com.meta.xr.sdk.core/Prefabs/";

        static Font font;
        static Material glowMat, additiveMat, textMat;
        static readonly Color DeckAColor = BoardColors.DeckA;
        static readonly Color DeckBColor = BoardColors.DeckB;
        static readonly Color LabelColor = new Color(0.75f, 0.78f, 0.85f);

        [MenuItem("DJ Master/Build Scene")]
        public static void Build()
        {
            Directory.CreateDirectory(GeneratedDir);
            Directory.CreateDirectory("Assets/Scenes");
            AssetDatabase.Refresh();

            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            textMat = font.material;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            QualitySettings.pixelLightCount = 2;
            // Smooth edges. Android (Quest) uses a quality level with MSAA off by default; 4x is what Meta recommends.
            int level = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.antiAliasing = 4;
            }
            QualitySettings.SetQualityLevel(level, false);

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Flat;
            Lightmapping.bakedGI = false;
            Lightmapping.realtimeGI = false;

            glowMat = MaterialAsset("Glow", "DJMaster/Glow");
            glowMat.color = new Color(1, 0.85f, 0.2f, 0.6f);
            additiveMat = MaterialAsset("Additive", "DJMaster/Additive");

            // ---------- World, audio, board ----------
            var world = new GameObject("World").transform;
            var anchor = world.gameObject.AddComponent<SeatedAnchor>();

            var audioRoot = new GameObject("Audio").transform;
            audioRoot.SetParent(world, false);
            var deckA = MakeDeck("Deck A", "A", audioRoot);
            var deckB = MakeDeck("Deck B", "B", audioRoot);
            var mixer = audioRoot.gameObject.AddComponent<Mixer>();
            mixer.decks = new[] { deckA, deckB };
            var judge = audioRoot.gameObject.AddComponent<MixJudge>();
            judge.mixer = mixer;
            var sfx = audioRoot.gameObject.AddComponent<Sfx>();
            sfx.uiSource = AudioChild("UI", audioRoot);
            sfx.crowdSource = AudioChild("Crowd", audioRoot);

            var board = BuildBoard(world, mixer, deckA, deckB, out var displays, out var sync);
            sync.judge = judge;
            sync.mixer = mixer;

            var panel = BuildPanel(board.transform);
            var builder = BuildSetBuilder(board.transform, deckA);
            var startMenu = BuildStartMenu(world);
            var vocab = BuildVocabPanel(world);
            var scenePanel = BuildScenePanel(board.transform, out var roomBtn, out var clubBtn, out var festBtn, out var recenterBtn, out var upBtn, out var downBtn);

            // ---------- Venues ----------
            var club = BuildClub(world);
            var festival = BuildFestival(world);
            var standIn = BuildEditorRoom(world);

            // ---------- Rig and hands ----------
            var rig = BuildRig(out var cam, out var head);
            anchor.head = head;

            var keyLight = new GameObject("Key Light").AddComponent<Light>();
            keyLight.type = LightType.Directional;
            keyLight.transform.rotation = Quaternion.Euler(55, -25, 0);
            keyLight.shadows = LightShadows.None;
            keyLight.color = new Color(1f, 0.96f, 0.92f);

            // ---------- Directors ----------
            var directors = new GameObject("Directors");
            var env = directors.AddComponent<EnvironmentManager>();
            env.cam = cam;
            env.keyLight = keyLight;
            env.passthrough = rig.GetComponent<OVRPassthroughLayer>();
            env.editorRoomStandIn = standIn;
            env.room = new EnvironmentManager.Look { root = null, ambient = new Color(0.55f, 0.55f, 0.6f), keyLight = 0.7f };
            env.club = new EnvironmentManager.Look { root = club, ambient = new Color(0.22f, 0.18f, 0.3f), fogColor = new Color(0.03f, 0.0f, 0.05f), fogDensity = 0.045f, keyLight = 0.25f };
            env.festival = new EnvironmentManager.Look { root = festival, skybox = NightSky(), ambient = new Color(0.25f, 0.25f, 0.38f), fogColor = new Color(0.03f, 0.03f, 0.09f), fogDensity = 0.008f, keyLight = 0.3f };
            env.roomButton = roomBtn; env.clubButton = clubBtn; env.festivalButton = festBtn;

            var lessons = directors.AddComponent<LessonDirector>();
            lessons.board = board; lessons.mixer = mixer; lessons.judge = judge;

            var recorder = cam.gameObject.AddComponent<MixRecorder>();
            recorder.replay = AudioChild("Replay", audioRoot);
            var performer = directors.AddComponent<SetPerformer>();
            performer.board = board; performer.mixer = mixer; performer.judge = judge; performer.recorder = recorder;

            var game = directors.AddComponent<GameDirector>();
            game.board = board; game.mixer = mixer; game.judge = judge; game.lessons = lessons;
            game.performer = performer; game.builder = builder; game.recorder = recorder; game.startMenu = startMenu; game.vocab = vocab;
            game.builtFrom = SourceHash();
            game.panel = panel; game.environment = env; game.anchor = anchor;
            game.roomButton = roomBtn; game.clubButton = clubBtn; game.festivalButton = festBtn;
            game.recenterButton = recenterBtn; game.raiseButton = upBtn; game.lowerButton = downBtn;

            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("[DJMaster] Scene built: " + ScenePath);
        }

        // ================= Audio =================

        static Deck MakeDeck(string name, string id, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0;
            go.AddComponent<ChannelStrip>();
            var d = go.AddComponent<Deck>();
            d.deckName = id;
            return d;
        }

        static AudioSource AudioChild(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 0;
            return s;
        }

        // ================= Board =================

        // Laid out like a modern two-deck controller: each deck has a big jog wheel on top, round CUE and PLAY
        // buttons at the front corner, a 2x4 grid of performance pads and a tempo fader on its right.
        // The mixer runs down the middle: EQ and FILTER knobs, headphone cue, channel faders, crossfader.
        static DJBoard BuildBoard(Transform world, Mixer mixer, Deck deckA, Deck deckB, out DeckDisplay[] displays, out SyncMeter sync)
        {
            var bodyMat = Lit("BoardBody", new Color(0.075f, 0.075f, 0.08f), 0.35f);
            var plateMat = Lit("BoardPlate", new Color(0.1f, 0.1f, 0.11f), 0.3f);
            var slotMat = Unlit("Slot", new Color(0.01f, 0.01f, 0.012f));
            var capMat = Unlit("Cap", Color.gray);
            var screenMat = Unlit("Screen", new Color(0.015f, 0.02f, 0.03f));
            var white = Unlit("White", new Color(0.95f, 0.95f, 0.95f));

            var boardGo = new GameObject("Board");
            var t = boardGo.transform;
            t.SetParent(world, false);
            t.localPosition = new Vector3(0, 0.82f, 0.40f);
            var board = boardGo.AddComponent<DJBoard>();
            board.mixer = mixer;

            Box("Body", t, new Vector3(0, -0.026f, 0.0175f), new Vector3(0.78f, 0.05f, 0.465f), bodyMat);
            Box("MixerPlate", t, new Vector3(0, -0.0005f, 0.0175f), new Vector3(0.24f, 0.002f, 0.455f), plateMat);
            Box("GrooveL", t, new Vector3(-0.121f, 0f, 0.0175f), new Vector3(0.003f, 0.002f, 0.455f), slotMat);
            Box("GrooveR", t, new Vector3(0.121f, 0f, 0.0175f), new Vector3(0.003f, 0.002f, 0.455f), slotMat);
            // Sits just behind the three screens, tilted 30 degrees back to face the player.
            Box("ScreenHousing", t, new Vector3(0, 0.045f, 0.276f), new Vector3(0.78f, 0.11f, 0.06f), bodyMat, 0, 30);

            board.channels = new DeckControls[2];
            displays = new DeckDisplay[2];
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1 : 1;
                string p = i == 0 ? "A" : "B";
                var deck = i == 0 ? deckA : deckB;
                var color = i == 0 ? DeckAColor : DeckBColor;
                float ox = i == 0 ? -0.265f : 0.235f; // jog wheel center; the tempo fader sits to its right on both decks

                var ch = new DeckControls { deck = deck };
                ch.jog = MakeJog($"{p}.jog", t, new Vector3(ox, 0.012f, 0.1f), deck, color, white);
                Label(t, p, new Vector3(ox + 0.095f, 0.001f, 0.2f), 0.0034f, color);

                ch.load = MakeButton($"{p}.load", "LOAD", t, new Vector3(ox - 0.08f, 0.008f, -0.027f), new Vector2(0.034f, 0.018f), capMat, color);
                ch.sync = MakeButton($"{p}.sync", "SYNC", t, new Vector3(ox - 0.035f, 0.008f, -0.027f), new Vector2(0.034f, 0.018f), capMat, color);
                ch.loop = MakeButton($"{p}.loop", "LOOP", t, new Vector3(ox + 0.01f, 0.008f, -0.027f), new Vector2(0.034f, 0.018f), capMat, BoardColors.Cue);
                ch.cue = MakeButton($"{p}.cue", "CUE", t, new Vector3(ox - 0.08f, 0.01f, -0.075f), new Vector2(0.038f, 0.038f), capMat, BoardColors.Cue, round: true);
                ch.play = MakeButton($"{p}.play", "PLAY", t, new Vector3(ox - 0.08f, 0.01f, -0.15f), new Vector2(0.038f, 0.038f), capMat, BoardColors.Play, round: true);

                // Performance pads: hot cues on top, samplers below.
                Label(t, "HOT CUE", new Vector3(ox + 0.011f, 0.001f, -0.05f), 0.00085f, LabelColor);
                Label(t, "SAMPLER", new Vector3(ox + 0.011f, 0.001f, -0.1125f), 0.00085f, LabelColor);
                string[] cueBars = { "1", "5", "9", "13" };
                for (int k = 0; k < 4; k++)
                {
                    float x = ox - 0.04f + k * 0.034f;
                    ch.hotCues[k] = MakeButton($"{p}.hotcue{k}", cueBars[k], t, new Vector3(x, 0.009f, -0.075f), new Vector2(0.025f, 0.025f), capMat, BoardColors.Cue,
                        out _, null, 0.0013f);
                    string sampleId = i == 0 && k == 0 ? "airhorn" : $"{p}.sampler{k}";
                    ch.samplers[k] = MakeButton(sampleId, Sfx.SampleNames[k], t, new Vector3(x, 0.009f, -0.15f), new Vector2(0.025f, 0.025f), capMat, BoardColors.Crowd,
                        out _, null, 0.00075f);
                }
                if (i == 0) board.airHorn = ch.samplers[0];

                float tx = ox + 0.115f;
                ch.tempo = MakeFader($"{p}.tempo", "TEMPO", t, new Vector3(tx, 0f, -0.095f), 0.13f, false, slotMat, capMat, 0.5f);
                ch.tempo.dragRatio = 0.35f;
                ch.tempo.detent = 0.5f;
                Label(t, "-", new Vector3(tx - 0.02f, 0.001f, -0.035f), 0.0018f, LabelColor);
                Label(t, "+", new Vector3(tx - 0.02f, 0.001f, -0.155f), 0.0018f, LabelColor);

                // Mixer channel
                float mx = side * 0.055f;
                float labelX = side * 0.095f;
                ch.high = MakeKnob($"{p}.high", t, new Vector3(mx, 0.02f, 0.205f), capMat, white, color);
                ch.mid = MakeKnob($"{p}.mid", t, new Vector3(mx, 0.02f, 0.165f), capMat, white, color);
                ch.low = MakeKnob($"{p}.low", t, new Vector3(mx, 0.02f, 0.125f), capMat, white, color);
                ch.filter = MakeKnob($"{p}.filter", t, new Vector3(mx, 0.02f, 0.075f), capMat, white, BoardColors.Fx);
                Label(t, "HI", new Vector3(labelX, 0.001f, 0.205f), 0.001f, LabelColor);
                Label(t, "MID", new Vector3(labelX, 0.001f, 0.165f), 0.001f, LabelColor);
                Label(t, "LOW", new Vector3(labelX, 0.001f, 0.125f), 0.001f, LabelColor);
                Label(t, "FILTER", new Vector3(labelX, 0.001f, 0.075f), 0.0009f, LabelColor);
                ch.headphones = MakeButton($"{p}.headphones", p, t, new Vector3(side * 0.017f, 0.008f, 0.16f), new Vector2(0.024f, 0.018f), capMat, BoardColors.Fx);
                ch.echo = MakeButton($"{p}.echo", "ECHO", t, new Vector3(mx, 0.008f, 0.033f), new Vector2(0.03f, 0.016f), capMat, BoardColors.Fx);
                ch.volume = MakeFader($"{p}.volume", "", t, new Vector3(mx, 0f, -0.075f), 0.085f, false, slotMat, capMat, 0f);
                Label(t, p, new Vector3(mx, 0.001f, -0.133f), 0.0018f, color);

                board.channels[i] = ch;
                displays[i] = MakeDeckDisplay(p, t, new Vector3(i == 0 ? -0.24f : 0.24f, 0.062f, 0.247f), deck, screenMat, color);
            }

            Label(t, "DJ MASTER", new Vector3(0, 0.001f, 0.215f), 0.0011f, Color.white);
            Label(t, "PHONES", new Vector3(0, 0.001f, 0.18f), 0.0008f, LabelColor);
            board.tap = MakeButton("tap", "TAP", t, new Vector3(0, 0.01f, -0.075f), new Vector2(0.03f, 0.03f), capMat, BoardColors.Crowd, round: true);
            board.crossfader = MakeFader("crossfader", "", t, new Vector3(0, 0f, -0.175f), 0.09f, true, slotMat, capMat, 0.5f);
            Label(t, "A", new Vector3(-0.065f, 0.001f, -0.175f), 0.0015f, DeckAColor);
            Label(t, "B", new Vector3(0.065f, 0.001f, -0.175f), 0.0015f, DeckBColor);

            sync = MakeSyncMeter(t, new Vector3(0, 0.062f, 0.247f), screenMat);
            return board;
        }

        static PokeButton MakeButton(string id, string label, Transform parent, Vector3 pos, Vector2 size, Material capMat, Color onColor, Quaternion? rot = null, bool round = false)
            => MakeButton(id, label, parent, pos, size, capMat, onColor, out _, rot, round: round);

        static PokeButton MakeButton(string id, string label, Transform parent, Vector3 pos, Vector2 size, Material capMat, Color onColor, out TextMeshPro labelText,
            Quaternion? rot = null, float labelSize = 0.0011f, Color? labelColor = null, bool round = false)
        {
            var go = new GameObject(id);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot ?? Quaternion.identity;
            var b = go.AddComponent<PokeButton>();
            b.id = id; b.label = label; b.size = size; b.onColor = onColor;
            b.offColor = Color.Lerp(new Color(0.1f, 0.1f, 0.11f), onColor, 0.3f); // a dim tint keeps the color code visible

            // Cylinders are 2 units tall, so halve their y scale to match the cubes.
            var shape = round ? PrimitiveType.Cylinder : PrimitiveType.Cube;
            float yScale = round ? 0.5f : 1f;
            var bezel = Prim(shape, "Bezel", go.transform, new Vector3(0, -0.011f, 0), new Vector3(size.x + 0.006f, 0.004f * yScale, size.y + 0.006f), Lit("Bezel", new Color(0.03f, 0.03f, 0.035f), 0.3f));
            if (round) // a colored ring around the cap, like the CUE and PLAY buttons on a real controller
                Prim(shape, "Ring", go.transform, new Vector3(0, -0.0085f, 0), new Vector3(size.x + 0.005f, 0.0015f * yScale, size.y + 0.005f), Unlit($"Ring{ColorUtility.ToHtmlStringRGB(onColor)}", onColor * 0.7f));
            var cap = Prim(shape, "Cap", go.transform, new Vector3(0, -0.005f, 0), new Vector3(size.x, 0.01f * yScale, size.y), capMat);
            b.cap = cap.transform;
            b.capRenderer = cap.GetComponent<Renderer>();
            b.glow = Prim(shape, "Glow", go.transform, new Vector3(0, -0.006f, 0), new Vector3(size.x + 0.016f, 0.008f * yScale, size.y + 0.016f), glowMat).GetComponent<Renderer>();
            labelText = null;
            if (label != null)
            {
                var tm = Text("Label", go.transform, new Vector3(0, 0.0005f, 0), labelSize, TextAnchor.MiddleCenter, labelColor ?? new Color(0.02f, 0.02f, 0.02f));
                tm.transform.localRotation = Quaternion.Euler(90, 0, 0);
                tm.text = label;
                tm.transform.SetParent(cap.transform, true); // ride down with the cap
                labelText = tm;
            }
            AddCollider(go, new Vector3(0, -0.006f, 0), new Vector3(size.x, 0.012f, size.y));
            return b;
        }

        static Fader MakeFader(string id, string label, Transform parent, Vector3 pos, float length, bool horizontal, Material slotMat, Material capMat, float value)
        {
            var go = new GameObject(id);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var f = go.AddComponent<Fader>();
            f.id = id; f.label = label; f.length = length; f.horizontal = horizontal; f.value = value;

            Vector3 along = horizontal ? new Vector3(length + 0.01f, 0.003f, 0.006f) : new Vector3(0.006f, 0.003f, length + 0.01f);
            Prim(PrimitiveType.Cube, "Slot", go.transform, new Vector3(0, -0.001f, 0), along, slotMat);
            var cap = new GameObject("Cap").transform;
            cap.SetParent(go.transform, false);
            cap.localPosition = new Vector3(0, 0.007f, 0);
            Vector3 capSize = horizontal ? new Vector3(0.014f, 0.016f, 0.026f) : new Vector3(0.026f, 0.016f, 0.014f);
            Prim(PrimitiveType.Cube, "CapBody", cap, Vector3.zero, capSize, Lit("FaderCap", new Color(0.12f, 0.12f, 0.13f), 0.5f));
            Vector3 stripe = horizontal ? new Vector3(0.002f, 0.001f, 0.026f) : new Vector3(0.026f, 0.001f, 0.002f);
            Prim(PrimitiveType.Cube, "Line", cap, new Vector3(0, 0.0085f, 0), stripe, Unlit("White", Color.white));
            f.cap = cap;
            Vector3 glowSize = horizontal ? new Vector3(length + 0.03f, 0.002f, 0.035f) : new Vector3(0.035f, 0.002f, length + 0.03f);
            f.glow = Prim(PrimitiveType.Cube, "Glow", go.transform, new Vector3(0, 0.0005f, 0), glowSize, glowMat).GetComponent<Renderer>();
            if (!string.IsNullOrEmpty(label))
                Label(go.transform, label, horizontal ? new Vector3(0, 0.001f, 0.022f) : new Vector3(0, 0.001f, -length * 0.5f - 0.014f), 0.0011f, LabelColor);
            AddCollider(go, new Vector3(0, -0.004f, 0), horizontal ? new Vector3(length + 0.02f, 0.008f, 0.03f) : new Vector3(0.03f, 0.008f, length + 0.02f));
            return f;
        }

        static Knob MakeKnob(string id, Transform parent, Vector3 pos, Material capMat, Material white, Color ringColor)
        {
            var go = new GameObject(id);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var k = go.AddComponent<Knob>();
            k.id = id;
            k.radius = 0.017f;
            Prim(PrimitiveType.Cylinder, "Skirt", go.transform, new Vector3(0, -0.019f, 0), new Vector3(0.036f, 0.001f, 0.036f), Unlit($"Ring{ColorUtility.ToHtmlStringRGB(ringColor)}", ringColor * 0.6f));
            Prim(PrimitiveType.Cylinder, "Body", go.transform, new Vector3(0, -0.01f, 0), new Vector3(0.028f, 0.01f, 0.028f), Lit("KnobBody", new Color(0.1f, 0.1f, 0.11f), 0.5f));
            var ind = new GameObject("Indicator").transform;
            ind.SetParent(go.transform, false);
            Prim(PrimitiveType.Cube, "Line", ind, new Vector3(0, 0.0003f, 0.008f), new Vector3(0.003f, 0.001f, 0.012f), white);
            k.indicator = ind;
            k.glow = Prim(PrimitiveType.Cylinder, "Glow", go.transform, new Vector3(0, -0.017f, 0), new Vector3(0.048f, 0.002f, 0.048f), glowMat).GetComponent<Renderer>();
            AddCollider(go, new Vector3(0, -0.01f, 0), new Vector3(0.03f, 0.02f, 0.03f));
            return k;
        }

        // A big jog wheel: a dark textured ring around a black platter with a ring in the deck's color.
        static JogWheel MakeJog(string id, Transform parent, Vector3 pos, Deck deck, Color color, Material white)
        {
            var go = new GameObject(id);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var j = go.AddComponent<JogWheel>();
            j.id = id; j.deck = deck; j.radius = 0.085f;
            var black = Lit("Platter", new Color(0.03f, 0.03f, 0.035f), 0.75f);
            Prim(PrimitiveType.Cylinder, "Well", go.transform, new Vector3(0, -0.0115f, 0), new Vector3(0.198f, 0.0005f, 0.198f), Unlit("Slot", new Color(0.01f, 0.01f, 0.012f)));
            Prim(PrimitiveType.Cylinder, "OuterRing", go.transform, new Vector3(0, -0.009f, 0), new Vector3(0.19f, 0.003f, 0.19f), Lit("JogRing", new Color(0.16f, 0.16f, 0.17f), 0.25f));
            for (int k = 0; k < 48; k++) // grip ridges
            {
                var ridge = Prim(PrimitiveType.Cube, "Ridge", go.transform, Vector3.zero, new Vector3(0.0015f, 0.001f, 0.012f), Lit("Ridge", new Color(0.09f, 0.09f, 0.1f), 0.2f));
                ridge.transform.localRotation = Quaternion.Euler(0, k * 7.5f, 0);
                ridge.transform.localPosition = ridge.transform.localRotation * new Vector3(0, -0.0055f, 0.087f);
            }
            var platter = new GameObject("Platter").transform;
            platter.SetParent(go.transform, false);
            Prim(PrimitiveType.Cylinder, "Disc", platter, new Vector3(0, -0.004f, 0), new Vector3(0.16f, 0.004f, 0.16f), black);
            Prim(PrimitiveType.Cylinder, "ColorRing", platter, new Vector3(0, 0.0001f, 0), new Vector3(0.088f, 0.0003f, 0.088f), Unlit($"Ring{ColorUtility.ToHtmlStringRGB(color)}", color));
            Prim(PrimitiveType.Cylinder, "Center", platter, new Vector3(0, 0.0003f, 0), new Vector3(0.078f, 0.0003f, 0.078f), black);
            Prim(PrimitiveType.Cube, "Marker", platter, new Vector3(0, 0.0006f, 0.052f), new Vector3(0.006f, 0.001f, 0.06f), white);
            Prim(PrimitiveType.Cylinder, "Hub", platter, new Vector3(0, 0.0006f, 0), new Vector3(0.016f, 0.0005f, 0.016f), Lit("Metal", new Color(0.55f, 0.56f, 0.6f), 0.8f));
            j.platter = platter;
            j.glow = Prim(PrimitiveType.Cylinder, "Glow", go.transform, new Vector3(0, -0.009f, 0), new Vector3(0.21f, 0.002f, 0.21f), glowMat).GetComponent<Renderer>();
            AddCollider(go, new Vector3(0, -0.005f, 0), new Vector3(0.17f, 0.01f, 0.17f));
            return j;
        }

        static DeckDisplay MakeDeckDisplay(string p, Transform parent, Vector3 pos, Deck deck, Material screenMat, Color color)
        {
            var go = new GameObject($"{p}.display");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(30, 0, 0);
            var d = go.AddComponent<DeckDisplay>();
            d.deck = deck;
            Box("Screen", go.transform, new Vector3(0, 0, 0.002f), new Vector3(0.205f, 0.088f, 0.002f), screenMat);
            d.title = Text("Title", go.transform, new Vector3(-0.095f, 0.041f, 0), 0.0014f, TextAnchor.UpperLeft, Color.white);
            d.bpm = Text("BPM", go.transform, new Vector3(-0.095f, 0.0f, 0), 0.0019f, TextAnchor.MiddleLeft, color);
            d.position = Text("Position", go.transform, new Vector3(-0.095f, -0.021f, 0), 0.0014f, TextAnchor.MiddleLeft, LabelColor);
            d.beatLights = new Renderer[4];
            for (int i = 0; i < 4; i++)
                d.beatLights[i] = Box($"Beat{i}", go.transform, new Vector3(-0.086f + i * 0.022f, -0.034f, 0), new Vector3(0.018f, 0.006f, 0.001f), Unlit("BeatLight", Color.gray)).GetComponent<Renderer>();
            Box("PhraseBack", go.transform, new Vector3(0, -0.041f, 0), new Vector3(0.19f, 0.003f, 0.001f), Unlit("PhraseBack", new Color(0.08f, 0.08f, 0.1f)));
            var pivot = new GameObject("PhrasePivot").transform;
            pivot.SetParent(go.transform, false);
            pivot.localPosition = new Vector3(-0.095f, -0.041f, -0.0005f);
            Box("Fill", pivot, new Vector3(0.095f, 0, 0), new Vector3(0.19f, 0.003f, 0.001f), Unlit($"Phrase{p}", color));
            d.phraseFill = pivot;
            var lvl = new GameObject("LevelPivot").transform;
            lvl.SetParent(go.transform, false);
            lvl.localPosition = new Vector3(0.094f, -0.03f, -0.0005f);
            d.levelFill = Box("Level", lvl, new Vector3(0, 0.03f, 0), new Vector3(0.006f, 0.06f, 0.001f), Unlit("Level", Color.green)).GetComponent<Renderer>();
            d.levelPivot = lvl;
            return d;
        }

        static SyncMeter MakeSyncMeter(Transform parent, Vector3 pos, Material screenMat)
        {
            var go = new GameObject("SyncMeter");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(30, 0, 0);
            var s = go.AddComponent<SyncMeter>();
            Box("Screen", go.transform, new Vector3(0, 0, 0.002f), new Vector3(0.13f, 0.088f, 0.002f), screenMat);
            Text("Heading", go.transform, new Vector3(0, 0.04f, 0), 0.0011f, TextAnchor.UpperCenter, LabelColor).text = "SYNC";
            s.label = Text("Label", go.transform, new Vector3(0, 0.022f, 0), 0.0015f, TextAnchor.UpperCenter, Color.white);
            Box("Track", go.transform, new Vector3(0, -0.012f, 0), new Vector3(0.1f, 0.006f, 0.001f), Unlit("PhraseBack", new Color(0.08f, 0.08f, 0.1f)));
            Box("Center", go.transform, new Vector3(0, -0.012f, -0.0004f), new Vector3(0.002f, 0.012f, 0.001f), Unlit("White", Color.white));
            s.needle = Box("Needle", go.transform, new Vector3(0, -0.012f, -0.0008f), new Vector3(0.008f, 0.01f, 0.001f), Unlit("Needle", new Color(1f, 0.8f, 0.2f))).transform;
            s.halfWidth = 0.048f;
            s.lockLight = Prim(PrimitiveType.Cylinder, "Lock", go.transform, new Vector3(0.05f, 0.033f, -0.0005f), new Vector3(0.01f, 0.0005f, 0.01f), Unlit("Lock", Color.gray)).GetComponent<Renderer>();
            s.lockLight.transform.localRotation = Quaternion.Euler(90, 0, 0);
            Text("CrowdLabel", go.transform, new Vector3(-0.058f, -0.03f, 0), 0.0009f, TextAnchor.MiddleLeft, LabelColor).text = "CROWD";
            var pivot = new GameObject("EnergyPivot").transform;
            pivot.SetParent(go.transform, false);
            pivot.localPosition = new Vector3(-0.058f, -0.038f, -0.0005f);
            s.energyFill = Box("Energy", pivot, new Vector3(0.058f, 0, 0), new Vector3(0.116f, 0.005f, 0.001f), Unlit("Energy", Color.magenta)).GetComponent<Renderer>();
            s.energyPivot = pivot;
            return s;
        }

        // ================= Panels =================

        static InstructionPanel BuildPanel(Transform board)
        {
            var go = new GameObject("InstructionPanel");
            go.transform.SetParent(board, false);
            // Kept low so you can still see the crowd over the top of it.
            go.transform.localPosition = new Vector3(0, 0.215f, 0.33f);
            go.transform.localRotation = Quaternion.Euler(10, 0, 0);
            var p = go.AddComponent<InstructionPanel>();
            Box("Back", go.transform, new Vector3(0, 0, 0.004f), new Vector3(0.64f, 0.23f, 0.006f), Lit("PanelBack", new Color(0.04f, 0.04f, 0.06f), 0.6f));
            Box("Edge", go.transform, new Vector3(0, 0.115f, 0.003f), new Vector3(0.64f, 0.004f, 0.007f), Unlit("PanelEdge", DeckAColor));
            p.title = Text("Title", go.transform, new Vector3(-0.3f, 0.103f, 0), 0.0021f, TextAnchor.UpperLeft, new Color(1f, 0.85f, 0.3f));
            p.body = Text("Body", go.transform, new Vector3(-0.3f, 0.07f, 0), 0.0015f, TextAnchor.UpperLeft, Color.white);
            p.feedback = Text("Feedback", go.transform, new Vector3(0, -0.04f, 0), 0.0019f, TextAnchor.MiddleCenter, new Color(0.3f, 1f, 0.5f));
            p.status = Text("Status", go.transform, new Vector3(-0.3f, -0.062f, 0), 0.0012f, TextAnchor.MiddleLeft, LabelColor);
            p.wrapChars = 64;

            var faceUser = Quaternion.Euler(-90, 0, 0); // button +Y toward the player
            p.actionButtons = new PokeButton[4];
            p.actionLabels = new TextMeshPro[4];
            for (int i = 0; i < 4; i++)
            {
                var b = MakeButton($"panel.action{i}", "", go.transform, new Vector3(-0.24f + i * 0.16f, -0.088f, -0.004f), new Vector2(0.145f, 0.034f), Unlit("Cap", Color.gray), DeckAColor,
                    out var label, faceUser, 0.0015f, Color.white);
                b.offColor = new Color(0.15f, 0.2f, 0.3f);
                p.actionButtons[i] = b;
                p.actionLabels[i] = label;
            }
            return p;
        }

        // Build mode: the crate of songs, the transition styles and the set so far. Sits where the instruction panel does.
        static SetBuilderPanel BuildSetBuilder(Transform board, Deck preview)
        {
            var go = new GameObject("SetBuilderPanel");
            go.transform.SetParent(board, false);
            go.transform.localPosition = new Vector3(0, 0.255f, 0.33f);
            go.transform.localRotation = Quaternion.Euler(10, 0, 0);
            var p = go.AddComponent<SetBuilderPanel>();
            p.preview = preview;
            var t = go.transform;
            var faceUser = Quaternion.Euler(-90, 0, 0);
            var capMat = Unlit("Cap", Color.gray);

            Box("Back", t, new Vector3(0, 0, 0.004f), new Vector3(0.64f, 0.31f, 0.006f), Lit("PanelBack", new Color(0.04f, 0.04f, 0.06f), 0.6f));
            Box("Edge", t, new Vector3(0, 0.155f, 0.003f), new Vector3(0.64f, 0.004f, 0.007f), Unlit("PanelEdgeB", DeckBColor));
            Text("Title", t, new Vector3(-0.3f, 0.145f, 0), 0.0021f, TextAnchor.UpperLeft, new Color(1f, 0.85f, 0.3f)).text = "BUILD YOUR SET";
            Text("HowTo", t, new Vector3(-0.3f, 0.114f, 0), 0.00125f, TextAnchor.UpperLeft, Color.white).text =
                "1  Pick a transition style     2  Tap songs to add them in order     3  GO LIVE";
            Text("StyleHeading", t, new Vector3(0.003f, 0.093f, 0), 0.00115f, TextAnchor.UpperLeft, LabelColor).text = "TRANSITION STYLE";

            PokeButton Btn(string id, Vector3 pos, Vector2 size, Color on, float labelSize, out TextMeshPro label)
            {
                var b = MakeButton(id, "", t, pos, size, capMat, on, out label, faceUser, labelSize, Color.white);
                b.offColor = new Color(0.15f, 0.15f, 0.22f);
                return b;
            }

            // The crate has two tabs: the game's own tracks and free (CC0) music.
            p.gameTab = Btn("crate.tab0", new Vector3(-0.232f, 0.093f, -0.004f), new Vector2(0.136f, 0.016f), DeckAColor, 0.0011f, out var gameTabLabel);
            gameTabLabel.text = "GAME TRACKS";
            p.freeTab = Btn("crate.tab1", new Vector3(-0.088f, 0.093f, -0.004f), new Vector2(0.136f, 0.016f), DeckAColor, 0.0011f, out var freeTabLabel);
            freeTabLabel.text = "FREE MUSIC (CC0)";
            for (int i = 0; i < 8; i++)
            {
                var pos = new Vector3(i % 2 == 0 ? -0.232f : -0.088f, 0.065f - (i / 2) * 0.04f, -0.004f);
                p.crateButtons[i] = Btn($"crate.{i}", pos, new Vector2(0.136f, 0.034f), DeckAColor, 0.0016f, out p.crateLabels[i]);
            }
            string[] styles = { "PHRASE MIX", "BASS SWAP", "FILTER MIX", "QUICK CUT", "FADE", "ECHO OUT" };
            for (int i = 0; i < styles.Length; i++)
            {
                var pos = new Vector3(i % 2 == 0 ? 0.04f : 0.122f, 0.065f - (i / 2) * 0.04f, -0.004f);
                p.styleButtons[i] = Btn($"style.{i}", pos, new Vector2(0.076f, 0.034f), BoardColors.Fx, 0.00125f, out var label);
                label.text = styles[i];
            }
            p.info = Text("Info", t, new Vector3(0.003f, -0.04f, 0), 0.00105f, TextAnchor.UpperLeft, LabelColor);
            p.setText = Text("SetList", t, new Vector3(0.175f, 0.093f, 0), 0.00135f, TextAnchor.UpperLeft, Color.white);
            p.status = Text("Status", t, new Vector3(-0.3f, -0.09f, 0), 0.0014f, TextAnchor.MiddleLeft, new Color(0.3f, 1f, 0.5f));

            PokeButton Action(int i, string label, Color on)
            {
                var b = Btn($"build.action{i}", new Vector3(-0.24f + i * 0.16f, -0.125f, -0.004f), new Vector2(0.145f, 0.034f), on, 0.0015f, out var tm);
                tm.text = label;
                return b;
            }
            p.back = Action(0, "BACK", Color.white);
            p.undo = Action(1, "UNDO", Color.white);
            p.clear = Action(2, "CLEAR", Color.white);
            p.goLive = Action(3, "GO LIVE", BoardColors.Play);
            return p;
        }

        // The start screen: a purple menu floating in the dark, within easy reach of a seated player.
        static StartMenu BuildStartMenu(Transform world)
        {
            var go = new GameObject("StartMenu");
            go.transform.SetParent(world, false);
            go.transform.localPosition = new Vector3(0, 1.0f, 0.5f);   // a little below eye height, an arm's length away
            go.transform.localRotation = Quaternion.Euler(20, 0, 0);
            var m = go.AddComponent<StartMenu>();
            var t = go.transform;
            var faceUser = Quaternion.Euler(-90, 0, 0);
            var purple = StartMenu.Purple;

            Box("Back", t, new Vector3(0, -0.005f, 0.004f), new Vector3(0.62f, 0.37f, 0.006f), Unlit("MenuBack", new Color(0.035f, 0.01f, 0.06f)));
            Box("EdgeTop", t, new Vector3(0, 0.18f, 0.003f), new Vector3(0.62f, 0.004f, 0.007f), Unlit("MenuEdge", purple));
            Box("EdgeBottom", t, new Vector3(0, -0.19f, 0.003f), new Vector3(0.62f, 0.004f, 0.007f), Unlit("MenuEdge", purple));
            m.title = Text("Title", t, new Vector3(0, 0.165f, 0), 0.0042f, TextAnchor.UpperCenter, purple);
            m.subtitle = Text("Subtitle", t, new Vector3(0, 0.105f, 0), 0.0017f, TextAnchor.UpperCenter, StartMenu.Lavender);
            m.footer = Text("Footer", t, new Vector3(0, -0.16f, 0), 0.0013f, TextAnchor.MiddleCenter, StartMenu.Lavender);
            m.wrapChars = 34;

            for (int i = 0; i < 4; i++)
            {
                float y = 0.045f - i * 0.05f;
                var b = MakeButton($"menu.{i}", "", t, new Vector3(-0.155f, y, -0.004f), new Vector2(0.22f, 0.04f), Unlit("Cap", Color.gray), purple,
                    out m.labels[i], faceUser, 0.0017f, Color.white);
                b.offColor = new Color(0.3f, 0.1f, 0.5f);
                m.buttons[i] = b;
                m.descriptions[i] = Text($"Description{i}", t, new Vector3(-0.025f, y, 0), 0.00145f, TextAnchor.MiddleLeft, purple);
            }
            return m;
        }

        // DJ VOCAB: a bigger purple board than the start menu, with every word listed in two columns
        // and a QUIZ ME button at the top. The quiz swaps the list for a question and four answers.
        static VocabPanel BuildVocabPanel(Transform world)
        {
            var go = new GameObject("VocabPanel");
            go.transform.SetParent(world, false);
            go.transform.localPosition = new Vector3(0, 1.02f, 0.62f);
            go.transform.localRotation = Quaternion.Euler(14, 0, 0);
            var v = go.AddComponent<VocabPanel>();
            var t = go.transform;
            var faceUser = Quaternion.Euler(-90, 0, 0);
            var purple = StartMenu.Purple;
            const float W = 0.86f, Top = 0.33f, Bottom = -0.33f;

            Box("Back", t, new Vector3(0, (Top + Bottom) / 2, 0.004f), new Vector3(W, Top - Bottom, 0.006f), Unlit("MenuBack", new Color(0.035f, 0.01f, 0.06f)));
            Box("EdgeTop", t, new Vector3(0, Top, 0.003f), new Vector3(W, 0.004f, 0.007f), Unlit("MenuEdge", purple));
            Box("EdgeBottom", t, new Vector3(0, Bottom, 0.003f), new Vector3(W, 0.004f, 0.007f), Unlit("MenuEdge", purple));
            Text("Title", t, new Vector3(0, Top - 0.015f, 0), 0.0036f, TextAnchor.UpperCenter, purple).text = "DJ VOCAB";

            PokeButton Btn(string id, string label, Vector3 pos, Vector2 size, out TextMeshPro text, float labelSize = 0.0016f)
            {
                var b = MakeButton(id, label, t, pos + new Vector3(0, 0, -0.004f), size, Unlit("Cap", Color.gray), purple, out text, faceUser, labelSize, Color.white);
                b.offColor = new Color(0.3f, 0.1f, 0.5f);
                return b;
            }
            v.quizButton = Btn("vocab.quiz", "QUIZ ME", new Vector3(0, Top - 0.085f, 0), new Vector2(0.22f, 0.04f), out v.quizButtonLabel);
            v.menuButton = Btn("vocab.menu", "MENU", new Vector3(W / 2 - 0.075f, Top - 0.04f, 0), new Vector2(0.11f, 0.035f), out _, 0.0013f);
            v.info = Text("Info", t, new Vector3(0, Top - 0.125f, 0), 0.0013f, TextAnchor.MiddleCenter, StartMenu.Lavender);

            // The list: 12 words per column.
            var list = new GameObject("List");
            list.transform.SetParent(t, false);
            v.listRoot = list;
            int count = DJVocab.Words.Length, perColumn = (count + 1) / 2;
            v.entries = new TextMeshPro[count];
            float listTop = Top - 0.15f, step = (listTop - Bottom - 0.03f) / perColumn;
            for (int i = 0; i < count; i++)
            {
                int col = i / perColumn, row = i % perColumn;
                v.entries[i] = Text($"Word{i}", list.transform, new Vector3(-W / 2 + 0.03f + col * (W / 2), listTop - row * step, 0), 0.0015f, TextAnchor.UpperLeft, StartMenu.Lavender);
            }
            v.wrapChars = 68;

            // The quiz.
            var quiz = new GameObject("Quiz");
            quiz.transform.SetParent(t, false);
            v.quizRoot = quiz;
            var q = quiz.transform;
            v.progress = Text("Progress", q, new Vector3(0, Top - 0.16f, 0), 0.0014f, TextAnchor.MiddleCenter, purple);
            v.question = Text("Question", q, new Vector3(0, Top - 0.19f, 0), 0.0019f, TextAnchor.UpperCenter, Color.white);
            v.questionWrapChars = 40;
            for (int i = 0; i < 4; i++)
            {
                var pos = new Vector3((i % 2 == 0 ? -1 : 1) * 0.15f, -0.03f - (i / 2) * 0.075f, 0);
                v.answers[i] = MakeButton($"vocab.answer{i}", "", q, pos + new Vector3(0, 0, -0.004f), new Vector2(0.26f, 0.055f), Unlit("Cap", Color.gray), BoardColors.Play,
                    out v.answerLabels[i], faceUser, 0.0019f, Color.white);
                v.answers[i].offColor = new Color(0.3f, 0.1f, 0.5f);
            }
            v.feedback = Text("Feedback", q, new Vector3(0, -0.185f, 0), 0.0017f, TextAnchor.MiddleCenter, Color.white);
            v.next = Btn("vocab.next", "NEXT", new Vector3(0, -0.25f, 0), new Vector2(0.2f, 0.04f), out v.nextLabel);
            v.next.transform.SetParent(q, true);

            quiz.SetActive(false);
            go.SetActive(false);
            return v;
        }

        static GameObject BuildScenePanel(Transform board, out PokeButton room, out PokeButton club, out PokeButton fest, out PokeButton recenter, out PokeButton up, out PokeButton down)
        {
            var go = new GameObject("ScenePanel");
            go.transform.SetParent(board, false);
            go.transform.localPosition = new Vector3(-0.44f, 0.1f, 0.12f);
            go.transform.localRotation = Quaternion.Euler(10, -38, 0);
            Box("Back", go.transform, new Vector3(0, 0, 0.004f), new Vector3(0.13f, 0.27f, 0.006f), Lit("PanelBack", new Color(0.04f, 0.04f, 0.06f), 0.6f));
            Text("Heading", go.transform, new Vector3(0, 0.125f, 0), 0.0014f, TextAnchor.UpperCenter, new Color(1f, 0.85f, 0.3f)).text = "SCENE";
            var faceUser = Quaternion.Euler(-90, 0, 0);
            PokeButton Btn(string id, string label, float y, float w = 0.1f, float x = 0)
            {
                var b = MakeButton(id, label, go.transform, new Vector3(x, y, -0.004f), new Vector2(w, 0.034f), Unlit("Cap", Color.gray), new Color(1f, 0.3f, 0.75f),
                    out _, faceUser, 0.0012f, Color.white);
                b.offColor = new Color(0.15f, 0.15f, 0.22f);
                return b;
            }
            room = Btn("scene.room", "MY ROOM", 0.085f);
            club = Btn("scene.club", "CLUB", 0.04f);
            fest = Btn("scene.festival", "FESTIVAL", -0.005f);
            recenter = Btn("scene.recenter", "RECENTER", -0.06f);
            up = Btn("scene.up", "BOARD UP", -0.1f, 0.048f, -0.026f);
            down = Btn("scene.down", "DOWN", -0.1f, 0.048f, 0.026f);
            recenter.onColor = up.onColor = down.onColor = Color.white;
            return go;
        }

        // ================= Venues =================

        static GameObject BuildClub(Transform world)
        {
            var root = new GameObject("Club");
            root.transform.SetParent(world, false);
            var r = root.transform;
            var black = Lit("ClubWall", new Color(0.02f, 0.02f, 0.025f), 0.2f);
            var floor = Lit("ClubFloor", new Color(0.04f, 0.04f, 0.05f), 0.85f);
            var boothMat = Lit("Booth", new Color(0.05f, 0.05f, 0.06f), 0.5f);
            var trussMat = Lit("Truss", new Color(0.25f, 0.25f, 0.28f), 0.7f);

            Box("BoothFloor", r, new Vector3(0, -0.05f, 0.2f), new Vector3(4.5f, 0.1f, 3f), floor);
            Box("BoothFront", r, new Vector3(0, 0.42f, 1.15f), new Vector3(2.6f, 0.95f, 0.12f), boothMat);
            Box("DJTable", r, new Vector3(0, 0.385f, 0.42f), new Vector3(1.3f, 0.77f, 0.6f), boothMat);
            Box("DanceFloor", r, new Vector3(0, -0.55f, 8.5f), new Vector3(16f, 0.1f, 14f), floor);
            Box("WallL", r, new Vector3(-8f, 2f, 6.5f), new Vector3(0.2f, 5f, 18f), black);
            Box("WallR", r, new Vector3(8f, 2f, 6.5f), new Vector3(0.2f, 5f, 18f), black);
            Box("WallFar", r, new Vector3(0, 2f, 15.5f), new Vector3(16f, 5f, 0.2f), black);
            Box("WallBack", r, new Vector3(0, 2f, -2.2f), new Vector3(16f, 5f, 0.2f), black);
            Box("Ceiling", r, new Vector3(0, 4.5f, 6.5f), new Vector3(16f, 0.2f, 18f), black);
            Box("TrussA", r, new Vector3(0, 4.2f, 3f), new Vector3(12f, 0.25f, 0.25f), trussMat);
            Box("TrussB", r, new Vector3(0, 4.2f, 8f), new Vector3(12f, 0.25f, 0.25f), trussMat);
            Box("Bar", r, new Vector3(-6.5f, 0.05f, 9f), new Vector3(1.2f, 1.1f, 6f), boothMat);

            var pulsers = new List<Renderer>();
            // LED floor tiles under the crowd
            for (int x = 0; x < 8; x++)
                for (int z = 0; z < 6; z++)
                    if ((x + z) % 2 == 0)
                        pulsers.Add(Box("Tile", r, new Vector3(-7f + x * 2f, -0.495f, 2.5f + z * 2.2f), new Vector3(1.9f, 0.01f, 2.1f), Unlit("LEDTile", Color.white)).GetComponent<Renderer>());
            // LED wall behind the DJ
            for (int x = 0; x < 10; x++)
                for (int y = 0; y < 4; y++)
                    pulsers.Add(Box("LED", r, new Vector3(-6.75f + x * 1.5f, 0.6f + y * 1.0f, -2.05f), new Vector3(1.45f, 0.95f, 0.05f), Unlit("LEDTile", Color.white)).GetComponent<Renderer>());
            // Light strip along the booth front, facing the DJ
            pulsers.Add(Box("BoothStrip", r, new Vector3(0, 0.9f, 1.08f), new Vector3(2.6f, 0.03f, 0.02f), Unlit("LEDTile", Color.white)).GetComponent<Renderer>());
            // Neon strips on the side walls
            for (int i = 0; i < 2; i++)
                pulsers.Add(Box("Neon", r, new Vector3(i == 0 ? -7.88f : 7.88f, 3.2f, 7f), new Vector3(0.04f, 0.06f, 16f), Unlit("LEDTile", Color.white)).GetComponent<Renderer>());

            var show = root.AddComponent<LightShow>();
            show.pulsers = pulsers.ToArray();
            MakeLasers(show, r, 6, i => new Vector3(-2.5f + i, 3.4f, -1.6f), Quaternion.LookRotation(new Vector3(0, -0.18f, 1)), 22f);
            MakeBeams(show, r, new[]
            {
                new Vector3(-4, 4.05f, 3), new Vector3(0, 4.05f, 3), new Vector3(4, 4.05f, 3),
                new Vector3(-4, 4.05f, 8), new Vector3(0, 4.05f, 8), new Vector3(4, 4.05f, 8),
            }, Quaternion.Euler(90, 0, 0), 5f, 0.9f);

            // Mirror ball with glints
            var ball = new GameObject("MirrorBall").transform;
            ball.SetParent(r, false);
            ball.localPosition = new Vector3(0, 3.7f, 6f);
            Prim(PrimitiveType.Sphere, "Ball", ball, Vector3.zero, Vector3.one * 0.5f, Lit("Mirror", new Color(0.6f, 0.6f, 0.65f), 0.95f));
            var glints = new GameObject("Glints");
            glints.transform.SetParent(ball, false);
            glints.AddComponent<MeshFilter>().sharedMesh = SaveMesh(MeshKit.Sparkles(160, 0.26f, 0.012f, false, 3), "Glints");
            glints.AddComponent<MeshRenderer>().sharedMaterial = AdditiveVariant("SparkleAdd", new Color(1, 1, 1, 1), 1);
            show.spinner = ball;

            show.lights = new[]
            {
                PointLight("Wash L", r, new Vector3(-4, 3.5f, 6), 2.2f, 12),
                PointLight("Wash R", r, new Vector3(4, 3.5f, 6), 2.2f, 12),
            };
            show.fireworks = new[]
            {
                Confetti(r, new Vector3(-3, 4f, 6)),
                Confetti(r, new Vector3(3, 4f, 6)),
            };

            var crowd = root.AddComponent<Crowd>();
            crowd.count = 90;
            crowd.areaMin = new Vector2(-7, 2.4f);
            crowd.areaMax = new Vector2(7, 14);
            crowd.floorY = -0.5f;
            crowd.bodyMaterial = CrowdMaterial("CrowdBody", new Color(0.12f, 0.1f, 0.14f));
            crowd.glowMaterial = AdditiveVariant("GlowStick", new Color(0.3f, 1f, 0.6f, 1f), 0, instanced: true);
            crowd.seed = 5;
            root.SetActive(false);
            return root;
        }

        static GameObject BuildFestival(Transform world)
        {
            var root = new GameObject("Festival");
            root.transform.SetParent(world, false);
            var r = root.transform;
            var stage = Lit("Stage", new Color(0.06f, 0.06f, 0.07f), 0.5f);
            var ground = Lit("Grass", new Color(0.03f, 0.05f, 0.035f), 0.1f);
            var trussMat = Lit("Truss", new Color(0.25f, 0.25f, 0.28f), 0.7f);
            var speaker = Lit("Speaker", new Color(0.02f, 0.02f, 0.02f), 0.3f);

            Box("StageFloor", r, new Vector3(0, -0.1f, -1.5f), new Vector3(26f, 0.2f, 9f), stage);
            Box("StageFace", r, new Vector3(0, -1.05f, 2.95f), new Vector3(26f, 1.9f, 0.1f), speaker);
            Box("DJTable", r, new Vector3(0, 0.385f, 0.42f), new Vector3(1.3f, 0.77f, 0.6f), stage);
            Box("Ground", r, new Vector3(0, -2.05f, 70f), new Vector3(220f, 0.1f, 140f), ground);
            foreach (float x in new[] { -12.5f, 12.5f })
            {
                Box("PillarFront", r, new Vector3(x, 5f, 2.5f), new Vector3(0.6f, 14f, 0.6f), trussMat);
                Box("PillarBack", r, new Vector3(x, 5f, -6f), new Vector3(0.6f, 14f, 0.6f), trussMat);
                Box("SpeakerStack", r, new Vector3(x * 0.78f, 1.5f, 1.8f), new Vector3(1.6f, 3f, 1.2f), speaker);
            }
            Box("RoofFront", r, new Vector3(0, 11.8f, 2.5f), new Vector3(25.6f, 0.7f, 0.7f), trussMat);
            Box("Roof", r, new Vector3(0, 12.2f, -1.75f), new Vector3(25.6f, 0.2f, 9f), speaker);

            var pulsers = new List<Renderer>();
            // Light bars on the stage frame, visible from the DJ booth
            for (int i = 0; i < 12; i++)
            {
                float x = -11f + i * 2f;
                pulsers.Add(Box("RoofBar", r, new Vector3(x, 11.35f, 2.3f), new Vector3(1.6f, 0.12f, 0.12f), Unlit("LEDTile", Color.white)).GetComponent<Renderer>());
            }
            foreach (float x in new[] { -12.1f, 12.1f })
                for (int y = 0; y < 6; y++)
                    pulsers.Add(Box("PillarLED", r, new Vector3(x, 0.8f + y * 1.8f, 2.15f), new Vector3(0.12f, 1.5f, 0.12f), Unlit("LEDTile", Color.white)).GetComponent<Renderer>());
            // Big side screens angled toward the crowd and the stage
            foreach (float x in new[] { -18f, 18f })
            {
                var screen = Box("SideScreen", r, new Vector3(x, 5f, 8f), new Vector3(7f, 4.5f, 0.2f), Unlit("LEDTile", Color.white), x < 0 ? 35f : -35f);
                pulsers.Add(screen.GetComponent<Renderer>());
                Box("ScreenPole", r, new Vector3(x, 1f, 8.3f), new Vector3(0.3f, 6f, 0.3f), trussMat);
            }
            // Giant LED wall behind the DJ
            for (int x = 0; x < 8; x++)
                for (int y = 0; y < 4; y++)
                    pulsers.Add(Box("LEDWall", r, new Vector3(-10.5f + x * 3f, 1.5f + y * 2.4f, -5.8f), new Vector3(2.9f, 2.3f, 0.1f), Unlit("LEDTile", Color.white)).GetComponent<Renderer>());
            // Stage-front strip
            pulsers.Add(Box("StageStrip", r, new Vector3(0, 0.02f, 2.85f), new Vector3(25f, 0.04f, 0.08f), Unlit("LEDTile", Color.white)).GetComponent<Renderer>());
            // Flags in the crowd
            var rng = new System.Random(9);
            for (int i = 0; i < 10; i++)
            {
                var pos = new Vector3(-20f + (float)rng.NextDouble() * 40f, 0f, 10f + (float)rng.NextDouble() * 28f);
                Box("FlagPole", r, pos + Vector3.up * 0.5f, new Vector3(0.05f, 5f, 0.05f), trussMat);
                pulsers.Add(Box("Flag", r, pos + new Vector3(0.45f, 2.6f, 0), new Vector3(0.9f, 0.6f, 0.02f), Unlit("LEDTile", Color.white)).GetComponent<Renderer>());
            }

            var show = root.AddComponent<LightShow>();
            show.pulsers = pulsers.ToArray();
            MakeLasers(show, r, 8, i => new Vector3(-7f + i * 2f, 0.3f, 2.8f), Quaternion.LookRotation(new Vector3(0, 0.1f, 1)), 70f);
            MakeBeams(show, r, new[]
            {
                new Vector3(-10, 11.2f, 2.6f), new Vector3(-5, 11.2f, 2.6f), new Vector3(5, 11.2f, 2.6f), new Vector3(10, 11.2f, 2.6f),
                new Vector3(-24, 0f, 30f), new Vector3(24, 0f, 30f), new Vector3(-30, 0f, 55f), new Vector3(30, 0f, 55f),
            }, Quaternion.identity, 45f, 4f, beamIndex => beamIndex < 4 ? Quaternion.Euler(55, 0, 0) : Quaternion.Euler(-70, beamIndex % 2 == 0 ? 20 : -20, 0));

            var stars = new GameObject("Stars");
            stars.transform.SetParent(r, false);
            stars.AddComponent<MeshFilter>().sharedMesh = SaveMesh(MeshKit.Sparkles(900, 300f, 0.7f, true, 4), "Stars");
            stars.AddComponent<MeshRenderer>().sharedMaterial = AdditiveVariant("StarAdd", new Color(0.9f, 0.9f, 1f, 1f), 1);
            Prim(PrimitiveType.Sphere, "Moon", r, new Vector3(-90f, 120f, 220f), Vector3.one * 14f, Unlit("Moon", new Color(0.85f, 0.85f, 0.8f)));

            show.fireworks = new[]
            {
                Firework(r, new Vector3(-14, 22, 45), new Color(1f, 0.3f, 0.6f), new Color(1f, 0.85f, 0.3f)),
                Firework(r, new Vector3(0, 26, 52), new Color(0.3f, 0.7f, 1f), new Color(1f, 1f, 1f)),
                Firework(r, new Vector3(14, 22, 45), new Color(0.5f, 1f, 0.4f), new Color(0.8f, 0.3f, 1f)),
            };
            show.lights = new[] { PointLight("Stage Wash", r, new Vector3(0, 9f, 6f), 1.6f, 30f) };

            var crowd = root.AddComponent<Crowd>();
            crowd.count = 420;
            crowd.areaMin = new Vector2(-24, 5f);
            crowd.areaMax = new Vector2(24, 42f);
            crowd.floorY = -2f;
            crowd.bodyMaterial = CrowdMaterial("CrowdBody", new Color(0.12f, 0.1f, 0.14f));
            crowd.glowMaterial = AdditiveVariant("PhoneLight", new Color(1f, 0.95f, 0.85f, 1f), 0, instanced: true);
            crowd.glowChance = 0.25f;
            crowd.seed = 8;
            root.SetActive(false);
            return root;
        }

        // A grey room with a desk so the board has context in the editor, where passthrough isn't available.
        static GameObject BuildEditorRoom(Transform world)
        {
            var root = new GameObject("EditorRoomStandIn");
            root.transform.SetParent(world, false);
            var r = root.transform;
            var wall = Lit("StandInWall", new Color(0.55f, 0.53f, 0.5f), 0.1f);
            var floorMat = Lit("StandInFloor", new Color(0.35f, 0.3f, 0.26f), 0.2f);
            var desk = Lit("StandInDesk", new Color(0.45f, 0.33f, 0.22f), 0.3f);
            Box("Floor", r, new Vector3(0, -0.05f, 1f), new Vector3(5f, 0.1f, 6f), floorMat);
            Box("WallFront", r, new Vector3(0, 1.4f, 2.6f), new Vector3(5f, 2.8f, 0.1f), wall);
            Box("WallLeft", r, new Vector3(-2.5f, 1.4f, 1f), new Vector3(0.1f, 2.8f, 6f), wall);
            Box("WallRight", r, new Vector3(2.5f, 1.4f, 1f), new Vector3(0.1f, 2.8f, 6f), wall);
            Box("Desk", r, new Vector3(0, 0.745f, 0.5f), new Vector3(1.4f, 0.05f, 0.75f), desk);
            Text("Note", r, new Vector3(0, 2.1f, 2.54f), 0.01f, TextAnchor.MiddleCenter, new Color(0.2f, 0.2f, 0.2f)).text =
                "EDITOR STAND-IN\nOn Quest you see your real room here (passthrough)";
            return root;
        }

        static void MakeLasers(LightShow show, Transform parent, int count, System.Func<int, Vector3> posOf, Quaternion rest, float length)
        {
            var mesh = SaveMesh(MeshKit.Laser(length, 0.05f), $"Laser{length:0}");
            var mat = AdditiveVariant("LaserAdd", Color.green, 3);
            show.lasers = new Transform[count];
            show.laserRenderers = new Renderer[count];
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject($"Laser{i}");
                go.transform.SetParent(parent, false);
                go.transform.localPosition = posOf(i);
                go.transform.localRotation = rest;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                show.lasers[i] = go.transform;
                show.laserRenderers[i] = mr;
            }
        }

        static void MakeBeams(LightShow show, Transform parent, Vector3[] positions, Quaternion rest, float length, float radius, System.Func<int, Quaternion> restOf = null)
        {
            var mesh = SaveMesh(MeshKit.Beam(16, length, radius), $"Beam{length:0}");
            var mat = AdditiveVariant("BeamAdd", Color.white, 2);
            var fixture = Lit("Fixture", new Color(0.05f, 0.05f, 0.05f), 0.5f);
            show.beams = new Transform[positions.Length];
            show.beamRenderers = new Renderer[positions.Length];
            for (int i = 0; i < positions.Length; i++)
            {
                var go = new GameObject($"Beam{i}");
                go.transform.SetParent(parent, false);
                go.transform.localPosition = positions[i];
                go.transform.localRotation = restOf != null ? restOf(i) : rest;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                Prim(PrimitiveType.Cube, "Head", go.transform, Vector3.zero, new Vector3(0.3f, 0.3f, 0.35f), fixture);
                show.beams[i] = go.transform;
                show.beamRenderers[i] = mr;
            }
        }

        static ParticleSystem Firework(Transform parent, Vector3 pos, Color a, Color b)
        {
            var ps = Particles("Firework", parent, pos);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 2.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(6f, 11f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
            main.gravityModifier = 0.35f;
            main.startColor = new ParticleSystem.MinMaxGradient(a, b);
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.3f;
            return ps;
        }

        static ParticleSystem Confetti(Transform parent, Vector3 pos)
        {
            var ps = Particles("Confetti", parent, pos);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 4.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.09f);
            main.gravityModifier = 0.08f;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.2f, 0.6f), 0), new GradientColorKey(new Color(0.2f, 0.8f, 1f), 0.5f), new GradientColorKey(new Color(1f, 0.9f, 0.2f), 1) },
                      new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 1) });
            main.startColor = new ParticleSystem.MinMaxGradient(g) { mode = ParticleSystemGradientMode.RandomColor };
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(3f, 0.1f, 4f);
            return ps;
        }

        static ParticleSystem Particles(string name, Transform parent, Vector3 pos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.maxParticles = 800;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission;
            em.rateOverTime = 0;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                         new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 0.6f), new GradientAlphaKey(0, 1) });
            col.color = fade;
            var pr = go.GetComponent<ParticleSystemRenderer>();
            pr.sharedMaterial = AdditiveVariant("ParticleAdd", Color.white, 1);
            pr.shadowCastingMode = ShadowCastingMode.Off;
            return ps;
        }

        static Material NightSky()
        {
            var m = MaterialAsset("NightSky", "Skybox/Procedural");
            m.SetFloat("_SunDisk", 0);
            m.SetFloat("_SunSize", 0);
            m.SetFloat("_AtmosphereThickness", 0.55f);
            m.SetColor("_SkyTint", new Color(0.15f, 0.12f, 0.35f));
            m.SetColor("_GroundColor", new Color(0.01f, 0.01f, 0.02f));
            m.SetFloat("_Exposure", 0.18f);
            return m;
        }

        // ================= Rig =================

        static GameObject BuildRig(out Camera cam, out Transform head)
        {
            var rigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CorePrefabs + "OVRCameraRig.prefab");
            var rig = (GameObject)PrefabUtility.InstantiatePrefab(rigPrefab);
            rig.name = "OVRCameraRig";
            var cameraRig = rig.GetComponent<OVRCameraRig>();

            var manager = rig.GetComponent<OVRManager>();
            var so = new SerializedObject(manager);
            so.FindProperty("isInsightPassthroughEnabled").boolValue = true;
            so.FindProperty("_trackingOriginType").intValue = (int)OVRManager.TrackingOrigin.FloorLevel;
            so.ApplyModifiedPropertiesWithoutUndo();
            rig.AddComponent<OVRPassthroughLayer>();

            var trackingSpace = rig.transform.Find("TrackingSpace");
            head = trackingSpace.Find("CenterEyeAnchor");
            cam = head.GetComponent<Camera>();
            cam.nearClipPlane = 0.02f;
            cam.farClipPlane = 600f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0, 0, 0, 0);

            var tipMat = MaterialAsset("FingerTip", "DJMaster/Glow");
            tipMat.color = new Color(0.6f, 0.9f, 1f, 0.6f);
            AddHand(trackingSpace.Find("LeftHandAnchor"), OVRHand.Hand.HandLeft, rig.transform, tipMat);
            AddHand(trackingSpace.Find("RightHandAnchor"), OVRHand.Hand.HandRight, rig.transform, tipMat);

            var mouse = rig.AddComponent<MousePointerSource>();
            mouse.cam = cam;
            mouse.lookPivot = trackingSpace;
            mouse.rigRoot = rig.transform;
            _ = cameraRig;
            return rig;
        }

        static void AddHand(Transform anchor, OVRHand.Hand which, Transform rigRoot, Material tipMat)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CorePrefabs + "OVRHandPrefab.prefab");
            var hand = (GameObject)PrefabUtility.InstantiatePrefab(prefab, anchor);
            bool left = which == OVRHand.Hand.HandLeft;
            hand.name = left ? "LeftHand" : "RightHand";

            var ovrHand = hand.GetComponent<OVRHand>();
            var so = new SerializedObject(ovrHand);
            so.FindProperty("HandType").intValue = (int)which;
            so.ApplyModifiedPropertiesWithoutUndo();

            var skel = hand.GetComponent<OVRSkeleton>();
            so = new SerializedObject(skel);
            so.FindProperty("_skeletonType").intValue = (int)(left ? OVRSkeleton.SkeletonType.XRHandLeft : OVRSkeleton.SkeletonType.XRHandRight);
            so.ApplyModifiedPropertiesWithoutUndo();

            var mesh = hand.GetComponent<OVRMesh>();
            so = new SerializedObject(mesh);
            so.FindProperty("_meshType").intValue = (int)(left ? OVRMesh.MeshType.XRHandLeft : OVRMesh.MeshType.XRHandRight);
            so.ApplyModifiedPropertiesWithoutUndo();

            var src = hand.AddComponent<HandPointerSource>();
            src.hand = ovrHand;
            src.skeleton = skel;
            var tip = Prim(PrimitiveType.Sphere, left ? "LeftTip" : "RightTip", rigRoot, Vector3.zero, Vector3.one * 0.012f, tipMat);
            src.tipMarker = tip.GetComponent<Renderer>();
        }

        // ================= Quest settings =================

        [MenuItem("DJ Master/Configure Player Settings for Quest")]
        public static void ConfigureQuest()
        {
            var android = NamedBuildTarget.Android;
            PlayerSettings.companyName = "DJ Master";
            PlayerSettings.productName = "DJ Master";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetApplicationIdentifier(android, "com.djmaster.vr");
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)32;

            var config = OVRProjectConfig.CachedProjectConfig;
            config.handTrackingSupport = OVRProjectConfig.HandTrackingSupport.ControllersAndHands;
            config.insightPassthroughSupport = OVRProjectConfig.FeatureSupport.Supported;
            OVRProjectConfig.CommitProjectConfig(config);
            Debug.Log("[DJMaster] Quest settings applied: hands + passthrough enabled. Now enable OpenXR + Meta XR in XR Plug-in Management (see README).");
        }

        // ================= Batch-mode helpers =================

        public static void BuildAndConfigure()
        {
            ConfigureQuest();
            Build();
        }

        public static void BuildAndPreview()
        {
            Build();
            var env = Object.FindAnyObjectByType<EnvironmentManager>();
            var world = Object.FindAnyObjectByType<SeatedAnchor>().transform;
            var menu = Object.FindAnyObjectByType<StartMenu>();
            menu.gameObject.SetActive(false); // rendered on its own below
            Vector3 eye = world.TransformPoint(new Vector3(0, 1.24f, 0));
            foreach (var mode in new[] { EnvMode.Room, EnvMode.Club, EnvMode.Festival })
            {
                SetPreviewMode(env, mode);
                RenderPreview($"Preview_{mode}.png", eye, new Vector3(28, 0, 0), 90f, mode != EnvMode.Room);
                RenderPreview($"Preview_{mode}_Crowd.png", eye, new Vector3(2, 0, 0), 90f, mode != EnvMode.Room);
            }
            SetPreviewMode(env, EnvMode.Room);
            Object.FindAnyObjectByType<SetBuilderPanel>().gameObject.SetActive(false);
            RenderPreview("Preview_Board.png", world.TransformPoint(new Vector3(0, 1.24f, 0.02f)), new Vector3(42, 0, 0), 62f, false);
            RenderPreview("Preview_Panel.png", world.TransformPoint(new Vector3(0, 1.24f, 0.0f)), new Vector3(8, 0, 0), 60f, false);

            // The start screen and Build mode, filled in the way they look at runtime.
            var panel = Object.FindAnyObjectByType<InstructionPanel>();
            var builder = Object.FindAnyObjectByType<SetBuilderPanel>(FindObjectsInactive.Include);
            menu.gameObject.SetActive(true);
            var boardGo = Object.FindAnyObjectByType<DJBoard>().gameObject;
            menu.Show("DJ MASTER", "Learn to DJ with just your hands", "New to DJing? Start with LEARN.");
            menu.SetRows(
                ("LEARN", "Three short lessons: your first mix, fades and cuts, then sync, loops and effects."),
                ("BUILD A SET", "Pick songs and transitions, then perform them live for a crowd."),
                ("FREESTYLE", "Just play. No timer, no rules."));
            var tint = new MaterialPropertyBlock(); // what PokeButton does at runtime
            tint.SetColor("_Color", menu.buttons[0].offColor);
            foreach (var b in menu.buttons) b.capRenderer.SetPropertyBlock(tint);
            boardGo.SetActive(false);
            env.editorRoomStandIn.SetActive(false);
            RenderPreview("Preview_Menu.png", world.TransformPoint(new Vector3(0, 1.24f, 0.0f)), new Vector3(20, 0, 0), 60f, false);
            foreach (var b in menu.buttons) b.capRenderer.SetPropertyBlock(null);
            menu.gameObject.SetActive(false);
            boardGo.SetActive(true);
            env.editorRoomStandIn.SetActive(true);

            panel.gameObject.SetActive(false);
            builder.gameObject.SetActive(true);
            builder.Set.Clear();
            builder.Set.Add(0, TransitionStyle.Blend);
            builder.Set.Add(1, TransitionStyle.BassSwap);
            builder.Set.Add(5, TransitionStyle.Cut);
            builder.PickStyle(TransitionStyle.Cut);
            builder.Refresh();
            RenderPreview("Preview_Builder.png", world.TransformPoint(new Vector3(0, 1.24f, 0.0f)), new Vector3(14, 0, 0), 60f, false);
            builder.ShowPage(1);
            RenderPreview("Preview_BuilderFree.png", world.TransformPoint(new Vector3(0, 1.24f, 0.0f)), new Vector3(14, 0, 0), 60f, false);
            // What a small editor Game view shows with the mouse camera (its FOV, a typical window size).
            float editorFov = Object.FindAnyObjectByType<MousePointerSource>().editorFov;
            RenderPreview("Preview_EditorView.png", world.TransformPoint(new Vector3(0, 1.24f, 0.0f)), new Vector3(16, 0, 0), editorFov, false, 1170, 716);
            builder.ShowPage(0);
            builder.gameObject.SetActive(false);

            // DJ VOCAB: the word list, then a quiz question after a wrong answer.
            var vocab = Object.FindAnyObjectByType<VocabPanel>(FindObjectsInactive.Include);
            boardGo.SetActive(false);
            env.editorRoomStandIn.SetActive(false);
            panel.gameObject.SetActive(false);
            vocab.gameObject.SetActive(true);
            vocab.FillList();
            vocab.ShowList();
            tint.SetColor("_Color", vocab.quizButton.offColor);
            foreach (var b in vocab.GetComponentsInChildren<PokeButton>(true)) b.capRenderer.SetPropertyBlock(tint);
            Vector3 vocabEye = world.TransformPoint(new Vector3(0, 1.24f, 0.0f));
            RenderPreview("Preview_Vocab.png", vocabEye, new Vector3(18, 0, 0), 64f, false);
            vocab.Seed(1);
            vocab.StartQuiz();
            vocab.Answer((vocab.CorrectChoice + 1) % 4);
            foreach (var b in vocab.answers)
            {
                var block = new MaterialPropertyBlock();
                block.SetColor("_Color", b.Lit ? b.onColor : b.offColor);
                b.capRenderer.SetPropertyBlock(block);
            }
            RenderPreview("Preview_VocabQuiz.png", vocabEye, new Vector3(18, 0, 0), 64f, false);
            foreach (var b in vocab.GetComponentsInChildren<PokeButton>(true)) b.capRenderer.SetPropertyBlock(null);
            vocab.gameObject.SetActive(false);
            boardGo.SetActive(true);
            env.editorRoomStandIn.SetActive(true);

            panel.gameObject.SetActive(true);
            RenderPreview("Preview_ScenePanel.png", world.TransformPoint(new Vector3(0, 1.24f, 0.0f)), new Vector3(15, -38, 0), 60f, false);
        }

        static void SetPreviewMode(EnvironmentManager env, EnvMode mode)
        {
            if (env.room.root != null) env.room.root.SetActive(mode == EnvMode.Room);
            env.club.root.SetActive(mode == EnvMode.Club);
            env.festival.root.SetActive(mode == EnvMode.Festival);
            env.editorRoomStandIn.SetActive(mode == EnvMode.Room);
            var look = mode == EnvMode.Club ? env.club : mode == EnvMode.Festival ? env.festival : env.room;
            RenderSettings.skybox = look.skybox;
            RenderSettings.ambientLight = look.ambient;
            RenderSettings.fog = look.fogDensity > 0;
            RenderSettings.fogColor = look.fogColor;
            RenderSettings.fogDensity = look.fogDensity;
            env.keyLight.intensity = look.keyLight;
            // Static preview: switch on the light show's emitters at mid energy.
            if (look.root != null)
            {
                var show = look.root.GetComponent<LightShow>();
                var block = new MaterialPropertyBlock();
                for (int i = 0; i < show.laserRenderers.Length; i++)
                {
                    block.SetColor("_Color", show.palette[i % 2]);
                    show.laserRenderers[i].SetPropertyBlock(block);
                }
                for (int i = 0; i < show.pulsers.Length; i++)
                {
                    block.SetColor("_Color", show.palette[i % 3] * 0.8f);
                    show.pulsers[i].SetPropertyBlock(block);
                }
                foreach (var b in show.beamRenderers)
                {
                    block.SetColor("_Color", new Color(0.6f, 0.4f, 1f, 0.2f));
                    b.SetPropertyBlock(block);
                }
            }
        }

        static void RenderPreview(string file, Vector3 pos, Vector3 euler, float fov, bool skybox, int width = 1280, int height = 800)
        {
            var go = new GameObject("PreviewCam");
            var cam = go.AddComponent<Camera>();
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(euler));
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.02f;
            cam.farClipPlane = 600f;
            cam.clearFlags = skybox && RenderSettings.skybox != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
            cam.targetTexture = rt;

            // Crowds are drawn procedurally at runtime: pose them mid-dance, with a lively crowd, for the snapshot.
            var temp = new List<GameObject>();
            foreach (var crowd in Object.FindObjectsByType<Crowd>())
            {
                if (!crowd.gameObject.activeInHierarchy) continue;
                crowd.Pose(1.3f, 0.12, true, 0.8f);
                crowd.Draw(cam);
            }

            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(), "Logs", file), tex.EncodeToPNG());
            foreach (var t in temp) Object.DestroyImmediate(t);
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(rt);
        }

        // ================= Helpers =================

        // TextMeshPro draws letters from signed distance fields, so they stay sharp at any size and distance
        // (TextMesh letters break up into pixels in VR). charSize is the old TextMesh character size; x64 keeps the same size.
        const float TmpScale = 64f;

        static TextMeshPro Text(string name, Transform parent, Vector3 localPos, float charSize, TextAnchor anchor, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tm = go.AddComponent<TextMeshPro>();
            var rt = tm.rectTransform;
            rt.sizeDelta = Vector2.zero; // no box: text grows out from the pivot, like TextMesh's anchor
            rt.pivot = anchor switch
            {
                TextAnchor.UpperLeft => new Vector2(0, 1), TextAnchor.UpperCenter => new Vector2(0.5f, 1), TextAnchor.UpperRight => new Vector2(1, 1),
                TextAnchor.MiddleLeft => new Vector2(0, 0.5f), TextAnchor.MiddleCenter => new Vector2(0.5f, 0.5f), TextAnchor.MiddleRight => new Vector2(1, 0.5f),
                TextAnchor.LowerLeft => new Vector2(0, 0), TextAnchor.LowerCenter => new Vector2(0.5f, 0), _ => new Vector2(1, 0),
            };
            go.transform.localPosition = localPos;
            tm.alignment = anchor switch
            {
                TextAnchor.UpperLeft => TextAlignmentOptions.TopLeft, TextAnchor.UpperCenter => TextAlignmentOptions.Top, TextAnchor.UpperRight => TextAlignmentOptions.TopRight,
                TextAnchor.MiddleLeft => TextAlignmentOptions.Left, TextAnchor.MiddleCenter => TextAlignmentOptions.Center, TextAnchor.MiddleRight => TextAlignmentOptions.Right,
                TextAnchor.LowerLeft => TextAlignmentOptions.BottomLeft, TextAnchor.LowerCenter => TextAlignmentOptions.Bottom, _ => TextAlignmentOptions.BottomRight,
            };
            tm.font = TMP_Settings.defaultFontAsset;
            tm.fontSize = charSize * TmpScale;
            tm.textWrappingMode = TextWrappingModes.NoWrap; // lines are broken by TextUtil.Wrap
            tm.overflowMode = TextOverflowModes.Overflow;
            tm.richText = true;
            tm.color = color;
            tm.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            return tm;
        }

        // Flat label printed on the board surface, readable from the player's seat.
        static TextMeshPro Label(Transform parent, string text, Vector3 pos, float size, Color color)
        {
            var tm = Text("Label", parent, pos, size, TextAnchor.MiddleCenter, color);
            tm.transform.localRotation = Quaternion.Euler(90, 0, 0);
            tm.text = text;
            return tm;
        }

        static void AddCollider(GameObject go, Vector3 center, Vector3 size)
        {
            // Every control is finished by the time its collider goes on: hide its lesson glow until a lesson asks for it.
            if (go.GetComponent<DJControl>() is { glow: { } g }) g.enabled = false;
            var c = go.AddComponent<BoxCollider>();
            c.center = center;
            c.size = size;
            c.isTrigger = true;
        }

        static Material Lit(string name, Color color, float smoothness)
        {
            var m = MaterialAsset(name, "Standard");
            m.color = color;
            m.SetFloat("_Glossiness", smoothness);
            return m;
        }

        static Material Unlit(string name, Color color)
        {
            var m = MaterialAsset(name, "Unlit/Color");
            m.color = color;
            return m;
        }

        static Material CrowdMaterial(string name, Color color)
        {
            // Per-person colors come from the Crowd script; the material color is only a fallback.
            var m = MaterialAsset(name, "DJMaster/CrowdInstanced");
            m.color = color;
            m.enableInstancing = true;
            return m;
        }

        static Material AdditiveVariant(string name, Color color, float shape, bool instanced = false)
        {
            var m = MaterialAsset(name, "DJMaster/Additive");
            m.color = color;
            m.SetFloat("_Shape", shape);
            m.enableInstancing = instanced;
            return m;
        }

        static Material MaterialAsset(string name, string shader)
        {
            string path = $"{GeneratedDir}/{name}.mat";
            var s = Shader.Find(shader);
            if (s == null) throw new System.Exception($"Shader not found: {shader}");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(s);
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = s;
            EditorUtility.SetDirty(m);
            return m;
        }

        static Mesh SaveMesh(Mesh mesh, string name)
        {
            string path = $"{GeneratedDir}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(mesh, existing);
                return existing;
            }
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, float rotY = 0, float rotX = 0)
        {
            var go = Prim(PrimitiveType.Cube, name, parent, pos, scale, mat);
            go.transform.localRotation = Quaternion.Euler(rotX, rotY, 0);
            return go;
        }

        static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        static Light PointLight(string name, Transform parent, Vector3 localPos, float intensity, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
            l.renderMode = LightRenderMode.ForcePixel;
            return l;
        }
    }
}
