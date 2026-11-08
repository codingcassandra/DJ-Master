using System;
using UnityEngine;
using UnityEngine.XR;

namespace DJMaster
{
    public enum EnvMode { Room, Club, Festival }

    // Switches between mixed reality (your real room via passthrough) and the two virtual venues.
    public class EnvironmentManager : MonoBehaviour
    {
        [Serializable]
        public class Look
        {
            public GameObject root;
            public Material skybox;
            public Color ambient = new Color(0.3f, 0.3f, 0.35f);
            public Color fogColor = Color.black;
            public float fogDensity;
            public float keyLight = 0.6f;
        }

        public Camera cam;
        [Tooltip("Soft directional light so the board reads well in every venue.")]
        public Light keyLight;
        public OVRPassthroughLayer passthrough;
        [Tooltip("Grey stand-in room shown in the editor, where there is no passthrough camera feed.")]
        public GameObject editorRoomStandIn;
        public Look room, club, festival;
        public PokeButton roomButton, clubButton, festivalButton;

        public EnvMode Mode { get; private set; }
        // The start screen: plain black everywhere, no passthrough, no venue.
        public bool MenuScreen { get; private set; }
        public event Action<EnvMode> Changed;

        void Start() => SetMode((EnvMode)PlayerPrefs.GetInt("env", (int)EnvMode.Room));

        public void SetMenuScreen(bool on)
        {
            MenuScreen = on;
            Apply();
        }

        public void SetMode(EnvMode mode)
        {
            Mode = mode;
            PlayerPrefs.SetInt("env", (int)mode);
            Apply();
            if (!MenuScreen && mode != EnvMode.Room && BeatBus.Playing) Sfx.Cheer(0.5f);
            Changed?.Invoke(mode);
        }

        void Apply()
        {
            bool mr = Mode == EnvMode.Room && !MenuScreen;
            if (passthrough != null) passthrough.hidden = !mr;
            if (editorRoomStandIn != null) editorRoomStandIn.SetActive(mr && !XRSettings.isDeviceActive);

            var look = Mode switch { EnvMode.Club => club, EnvMode.Festival => festival, _ => room };
            foreach (var l in new[] { room, club, festival })
                if (l.root != null) l.root.SetActive(l == look && !MenuScreen);

            // Passthrough shows through wherever the eye buffer is transparent.
            bool sky = !MenuScreen && !mr && look.skybox != null;
            cam.clearFlags = sky ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            cam.backgroundColor = mr ? new Color(0, 0, 0, 0) : Color.black;
            RenderSettings.skybox = MenuScreen ? null : look.skybox;
            RenderSettings.ambientLight = MenuScreen ? new Color(0.25f, 0.2f, 0.3f) : look.ambient;
            RenderSettings.fog = !MenuScreen && look.fogDensity > 0;
            RenderSettings.fogColor = look.fogColor;
            RenderSettings.fogDensity = look.fogDensity;
            if (keyLight != null) keyLight.intensity = MenuScreen ? 0.4f : look.keyLight;

            if (roomButton != null) roomButton.Lit = Mode == EnvMode.Room;
            if (clubButton != null) clubButton.Lit = Mode == EnvMode.Club;
            if (festivalButton != null) festivalButton.Lit = Mode == EnvMode.Festival;
        }
    }
}
