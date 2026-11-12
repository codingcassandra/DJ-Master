using UnityEngine;
#if DJ_MRUK
using System;
using System.Collections;
using System.Collections.Generic;
using Meta.XR.MRUtilityKit;
using UnityEngine.XR;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif
#endif

namespace DJMaster
{
    // In MY ROOM we use the room scan from Meta's MR Utility Kit to put the board on your real desk
    // If there's no scan, no table or no permission, the board just stays where SeatedAnchor puts it
    // If the MRUK package isn't there this does nothing
    public class DeskSnap : MonoBehaviour
    {
#if DJ_MRUK
        SeatedAnchor anchor;
        EnvironmentManager environment;
        readonly List<DeskPlacement.Table> tables = new List<DeskPlacement.Table>();
        bool ready, busy, onDesk;
#if UNITY_ANDROID && !UNITY_EDITOR
        bool asked;
        volatile bool granted;
#endif
#endif

        public void Init(SeatedAnchor seat, EnvironmentManager env)
        {
#if DJ_MRUK
            anchor = seat;
            environment = env;
            anchor.Recentered += OnRecentered;
            environment.Changed += OnModeChanged;
#endif
        }

#if DJ_MRUK
        void OnDestroy()
        {
            if (anchor != null) anchor.Recentered -= OnRecentered;
            if (environment != null) environment.Changed -= OnModeChanged;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        void Update()
        {
            // The permission answer can come in off the main thread, so we load from here
            if (!granted) return;
            granted = false;
            Load();
        }
#endif

        // SeatedAnchor just put the board in front of you, in MY ROOM we move it onto the desk if we can
        void OnRecentered()
        {
            if (environment.Mode != EnvMode.Room) { onDesk = false; return; }
            if (ready) onDesk = Snap();
            else TryLoad();
        }

        void OnModeChanged(EnvMode mode)
        {
            if (mode == EnvMode.Room)
            {
                if (ready) onDesk = Snap();
                else TryLoad();
            }
            else if (onDesk)
            {
                // Club and festival go back to the normal spot in front of you
                onDesk = false;
                anchor.Recenter();
            }
        }

        void TryLoad()
        {
            if (ready || busy) return;
            // In the editor without a headset there's no room to scan, the stand-in desk is enough
            if (!XRSettings.isDeviceActive) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!Permission.HasUserAuthorizedPermission(OVRPermissionsRequester.ScenePermission))
            {
                // We only ask once, if they say no the board stays in front of them
                if (asked) return;
                asked = true;
                var callbacks = new PermissionCallbacks();
                callbacks.PermissionGranted += _ => granted = true;
                callbacks.PermissionDenied += _ => Debug.Log("[DJMaster] No room scan permission, board stays in front of you");
                Permission.RequestUserPermission(OVRPermissionsRequester.ScenePermission, callbacks);
                return;
            }
#endif
            Load();
        }

        async void Load()
        {
            if (ready || busy) return;
            busy = true;
            try
            {
                var mruk = MRUK.Instance != null ? MRUK.Instance : MakeMruk();
                // false = don't pop up room setup if there's no scan, we just keep the normal placement
                var result = await mruk.LoadSceneFromDevice(requestSceneCaptureIfNoDataFound: false);
                if (this == null) return;
                if (result == MRUK.LoadDeviceResult.Success)
                {
                    ready = true;
                    StartCoroutine(RecenterSoon());
                }
                else Debug.Log($"[DJMaster] No room scan ({result}), board stays in front of you");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[DJMaster] Room scan didn't load, board stays in front of you: " + e.Message);
            }
            finally
            {
                busy = false;
            }
        }

        static MRUK MakeMruk()
        {
            // Start it turned off so MRUK's Awake sees our settings, we load the room ourselves after the permission
            var go = new GameObject("MRUK");
            go.SetActive(false);
            var mruk = go.AddComponent<MRUK>();
            mruk.SceneSettings = new MRUK.MRUKSettings { LoadSceneOnStartup = false };
            go.SetActive(true);
            return mruk;
        }

        IEnumerator RecenterSoon()
        {
            // Give MRUK a couple of frames to line the room up before we look at where your head is
            yield return null;
            yield return null;
            if (environment.Mode == EnvMode.Room) anchor.Recenter();
        }

        bool Snap()
        {
            var mruk = MRUK.Instance;
            if (mruk == null || anchor.head == null) return false;
            tables.Clear();
            foreach (var room in mruk.Rooms)
            {
                if (room == null) continue;
                var floors = room.FloorAnchors;
                float floorY = floors.Count > 0 && floors[0] != null ? floors[0].transform.position.y : float.NaN;
                foreach (var a in room.Anchors)
                    if (a != null && a.HasAnyLabel(MRUKAnchor.SceneLabels.TABLE) && ToTable(a, floorY, out var table)) tables.Add(table);
            }
            if (!DeskPlacement.PickBest(anchor.head.position, anchor.head.forward, tables, anchor.boardHeight, anchor.boardForward,
                    out var pos, out var rot)) return false;
            anchor.transform.SetPositionAndRotation(pos, rot);
            return true;
        }

        static readonly Vector3[] Axes = { Vector3.right, Vector3.up, Vector3.forward };

        // Turns an MRUK table into the simple table DeskPlacement wants. We don't trust which way the anchor's
        // axes point, so we find the one pointing up and use the other two for the table top
        static bool ToTable(MRUKAnchor a, float floorY, out DeskPlacement.Table table)
        {
            table = default;
            var t = a.transform;
            int up = 0;
            float best = 0;
            for (int i = 0; i < 3; i++)
            {
                float d = Mathf.Abs(Vector3.Dot(t.TransformDirection(Axes[i]), Vector3.up));
                if (d > best) { best = d; up = i; }
            }
            if (best < 0.9f) return false; // too tilted to be a table top

            Vector3 top, size;
            if (a.PlaneRect.HasValue && up == 2)
            {
                // The plane is the table top itself
                var r = a.PlaneRect.Value;
                top = t.TransformPoint(new Vector3(r.center.x, r.center.y, 0));
                size = new Vector3(r.width, r.height, 0);
            }
            else if (a.VolumeBounds.HasValue)
            {
                // A box, so the top is whichever end of the up axis is higher
                var b = a.VolumeBounds.Value;
                Vector3 half = Axes[up] * b.extents[up];
                Vector3 p1 = t.TransformPoint(b.center + half), p2 = t.TransformPoint(b.center - half);
                top = p1.y > p2.y ? p1 : p2;
                size = b.size;
            }
            else return false;

            int u = (up + 1) % 3, v = (up + 2) % 3;
            Vector3 edgeU = t.TransformVector(Axes[u] * size[u]);
            Vector3 edgeV = t.TransformVector(Axes[v] * size[v]);
            edgeU.y = 0;
            edgeV.y = 0;
            if (edgeU.sqrMagnitude < 1e-6f || edgeV.sqrMagnitude < 1e-6f) return false;
            table = new DeskPlacement.Table(top, Quaternion.LookRotation(edgeV.normalized, Vector3.up),
                new Vector2(edgeU.magnitude, edgeV.magnitude), floorY);
            return true;
        }
#endif
    }
}
