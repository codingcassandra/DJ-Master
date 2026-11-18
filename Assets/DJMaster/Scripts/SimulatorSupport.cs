// Meta XR Simulator support for the macOS build (see run-sim.sh). Only exists in a Mac player, and only switches on
// when the OpenXR runtime is the simulator, so the Quest build and the editor never see it.
//  - keeps the game running while the simulator window has focus (the game pauses itself when unfocused)
//  - undoes the desktop eye-height lift / pitch that MousePointerSource applies because XR comes up a frame late
//  - recenters you at the seated DJ spot on start, and again whenever Logs/recenter is touched
//  - mouse = right hand: the hand's index fingertip follows the cursor over the simulator view, a click (the
//    simulator sends it as a pinch) presses, hold and drag for faders. Tools/simwin tracks the simulator window.
//  - logs camera/scene/hand state every 3 s as "SIMDIAG" lines in Logs/player.log
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
using UnityEngine;
using UnityEngine.XR;

public class SimulatorSupport : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        var rt = System.Environment.GetEnvironmentVariable("XR_RUNTIME_JSON") ?? "";
        if (!rt.Contains("MetaXRSimulator")) return;
        var go = new GameObject("SimulatorSupport"); DontDestroyOnLoad(go); go.AddComponent<SimulatorSupport>();
    }

    // Mouse finger: the Mac mouse moves a fingertip dot over the scene (read globally, since the simulator has focus).
    // A click arrives from the simulator as a hand pinch, which presses whatever the dot is on; hold and drag for faders.
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    struct CGPoint { public double x, y; }
    [System.Runtime.InteropServices.DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")] static extern System.IntPtr CGEventCreate(System.IntPtr src);
    [System.Runtime.InteropServices.DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")] static extern CGPoint CGEventGetLocation(System.IntPtr ev);
    [System.Runtime.InteropServices.DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")] static extern void CFRelease(System.IntPtr p);
    static Vector2 MouseNow()
    {
        var e = CGEventCreate(System.IntPtr.Zero);
        var pt = CGEventGetLocation(e);
        CFRelease(e);
        return new Vector2((float)pt.x, (float)pt.y);
    }

    static readonly string WinFile = System.IO.Path.Combine(Application.dataPath, "../../../../Logs/simwindow.txt");
    Rect win; float nextWinRead;
    DJMaster.Pointer finger;
    Transform dot;
    bool down, wasPinch; Plane plane; Vector3 normal;
    OVRHand[] hands;
    // The right hand follows the mouse: its index fingertip is moved onto the cursor point.
    OVRHand rightHand; OVRSkeleton rightSkel; DJMaster.HandPointerSource rightSrc;
    bool handFollow; Vector3 handTarget;

    static Transform Tip(OVRSkeleton sk)
    {
        if (sk == null || !sk.IsInitialized || sk.Bones == null) return null;
        var t = sk.GetSkeletonType();
        bool xr = t == OVRSkeleton.SkeletonType.XRHandLeft || t == OVRSkeleton.SkeletonType.XRHandRight;
        var id = xr ? OVRSkeleton.BoneId.XRHand_IndexTip : OVRSkeleton.BoneId.Hand_IndexTip;
        foreach (var b in sk.Bones) if (b.Id == id) return b.Transform;
        return null;
    }

    void FindRightHand()
    {
        if (rightHand != null) return;
        foreach (var h in FindObjectsByType<OVRHand>(FindObjectsSortMode.None))
            if (h.name == "RightHand") { rightHand = h; rightSkel = h.GetComponent<OVRSkeleton>(); rightSrc = h.GetComponent<DJMaster.HandPointerSource>(); }
        if (rightSrc != null)
        {
            if (rightSrc.tipMarker != null) rightSrc.tipMarker.enabled = false;
            rightSrc.enabled = false; // the mouse finger pointer does the pressing for this hand
        }
        Application.onBeforeRender += PlaceHand;
    }

    void PlaceHand()
    {
        if (rightHand == null) return;
        if (!handFollow) { rightHand.transform.localPosition = Vector3.zero; return; }
        var tip = Tip(rightSkel);
        if (tip == null) return;
        rightHand.transform.position += handTarget - tip.position;
    }

    void OnDestroy() { Application.onBeforeRender -= PlaceHand; }


    void UpdateFinger()
    {
        var cam = Camera.main;
        if (cam == null) return;
        finger ??= DJMaster.Pointers.Create();
        if (hands == null || hands.Length == 0) hands = FindObjectsByType<OVRHand>(FindObjectsSortMode.None);
        if (dot == null)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(g.GetComponent<Collider>());
            g.name = "MouseFinger";
            g.transform.localScale = Vector3.one * 0.02f;
            var sh = Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Standard");
            var mat = new Material(sh); mat.color = new Color(1f, 0.85f, 0.2f);
            g.GetComponent<Renderer>().material = mat;
            dot = g.transform;
        }

        // Where is the simulator's 3D view on screen? Tools/simwin keeps the window bounds in Logs/simwindow.txt.
        if (Time.unscaledTime >= nextWinRead)
        {
            nextWinRead = Time.unscaledTime + 0.3f;
            try
            {
                var parts = System.IO.File.ReadAllText(WinFile).Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 4) win = new Rect(float.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture), float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture), float.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture), float.Parse(parts[3], System.Globalization.CultureInfo.InvariantCulture));
            }
            catch { }
        }
        FindRightHand();
        dot.GetComponent<Renderer>().enabled = false; // the hand is the cursor now
        handFollow = false;
        if (win.width <= 0) { finger.Active = false; PlaceHand(); return; }
        // The view is aspect-fit (left-eye image) inside the window, right of the 372 pt side panel, below the 92 pt toolbar.
        var area = new Rect(win.x + 372f, win.y + 92f, win.width - 372f - 15.5f, win.height - 92f - 34.5f);
        float aspect = XRSettings.eyeTextureHeight > 0 ? (float)XRSettings.eyeTextureWidth / XRSettings.eyeTextureHeight : 0.955f;
        float vw = Mathf.Min(area.width, area.height * aspect), vh = vw / aspect;
        var view = new Rect(area.center.x - vw / 2f, area.center.y - vh / 2f, vw, vh);

        var m = MouseNow();
        float u = (m.x - view.x) / view.width, v = (m.y - view.y) / view.height;
        bool inside = u >= 0 && u <= 1 && v >= 0 && v <= 1;
        if (!inside && !down) { finger.Active = false; PlaceHand(); return; }
        dot.gameObject.SetActive(true);

        // Unproject the cursor through the left eye, which is what the simulator view shows.
        var P = cam.GetStereoProjectionMatrix(Camera.StereoscopicEye.Left);
        var V = cam.GetStereoViewMatrix(Camera.StereoscopicEye.Left);
        var inv = (P * V).inverse;
        float nx = u * 2f - 1f, ny = 1f - v * 2f;
        var a = inv.MultiplyPoint(new Vector3(nx, ny, -1f));
        var b = inv.MultiplyPoint(new Vector3(nx, ny, 1f));
        var ray = new Ray(a, (b - a).normalized);

        bool pinch = false;
        foreach (var h in hands) if (h != null && h.IsTracked && h.GetFingerIsPinching(OVRHand.HandFinger.Index)) pinch = true;
        if (pinch != wasPinch) Debug.Log($"SIMDIAG pinch {pinch}");
        wasPinch = pinch;

        if (!pinch)
        {
            down = false; finger.Active = false;
            bool hit = Physics.Raycast(ray, out var hov, 4f);
            dot.position = hit ? hov.point - ray.direction * 0.015f : ray.GetPoint(0.5f);
            handTarget = dot.position; handFollow = true; PlaceHand();
            return;
        }
        if (!down)
        {
            if (!Physics.Raycast(ray, out var h0, 4f)) { finger.Active = false; dot.position = ray.GetPoint(0.5f); handTarget = dot.position; handFollow = true; PlaceHand(); return; }
            down = true; normal = h0.normal; plane = new Plane(h0.normal, h0.point);
            Debug.Log($"SIMDIAG press {h0.collider.name}");
        }
        finger.DirectPress = true;
        finger.Active = true;
        if (plane.Raycast(ray, out float dist)) { finger.Position = ray.GetPoint(dist) - normal * 0.009f; dot.position = ray.GetPoint(dist); }
        handTarget = dot.position; handFollow = true; PlaceHand();
    }

    float next, recenterAt = -1f, nextPoll;
    static readonly string Trigger = System.IO.Path.Combine(Application.dataPath, "../../../../Logs/recenter");

    // Put the headset at the seated DJ spot (eye height 1.24 m, facing +Z), moving the rig so hands come along.
    void Recenter()
    {
        var m = FindAnyObjectByType<DJMaster.MousePointerSource>();
        var cam = Camera.main;
        if (m == null || m.rigRoot == null || cam == null) return;
        var rig = m.rigRoot;
        var f = cam.transform.forward; f.y = 0;
        if (f.sqrMagnitude > 1e-4f)
        {
            float yaw = Vector3.SignedAngle(f, Vector3.forward, Vector3.up);
            rig.RotateAround(cam.transform.position, Vector3.up, yaw);
        }
        rig.position += new Vector3(0f, 1.24f, 0f) - cam.transform.position;
        Debug.Log($"SIMDIAG recentered: cam now {cam.transform.position} fwd {cam.transform.forward}");
    }
    // On a Mac the simulator window owns focus, so the game would pause itself forever. Keep it running.
    bool undone;
    void LateUpdate()
    {
        if (Time.timeScale == 0f) Time.timeScale = 1f; AudioListener.pause = false;
        // XR comes up a frame late here, so MousePointerSource.Start applied its desktop eye-height lift and
        // downward pitch to the rig. Undo both once XR is live, like on a Quest.
        if (!undone && XRSettings.isDeviceActive)
        {
            undone = true;
            var m = FindAnyObjectByType<DJMaster.MousePointerSource>();
            if (m != null)
            {
                if (m.rigRoot != null) m.rigRoot.position -= Vector3.up * m.editorEyeHeight;
                if (m.lookPivot != null) m.lookPivot.localRotation = Quaternion.identity;
                Debug.Log("SIMDIAG undid desktop rig lift/pitch");
            }
            recenterAt = Time.unscaledTime + 1.5f;
        }
        UpdateFinger();
        if (recenterAt > 0 && Time.unscaledTime >= recenterAt) { recenterAt = -1f; Recenter(); }
        if (Time.unscaledTime >= nextPoll)
        {
            nextPoll = Time.unscaledTime + 0.5f;
            if (System.IO.File.Exists(Trigger)) { System.IO.File.Delete(Trigger); Recenter(); }
        }
    }

    void Update()
    {
        if (Time.unscaledTime < next) return;
        next = Time.unscaledTime + 3f;
        var cams = Camera.allCameras;
        string s = $"SIMDIAG t={Time.time:F1} xr={XRSettings.isDeviceActive} dev={XRSettings.loadedDeviceName} cams={cams.Length}";
        foreach (var c in cams)
            s += $"\n  cam {c.name} en={c.enabled} pos={c.transform.position} fwd={c.transform.forward} clear={c.clearFlags} bg={c.backgroundColor} mask={c.cullingMask} near={c.nearClipPlane} far={c.farClipPlane} stereo={c.stereoEnabled} tgt={c.targetTexture}";
        int visible = 0, total = 0;
        foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None)) { total++; if (r.isVisible) visible++; }
        s += $"\n  renderers total={total} visible={visible}";
        foreach (var n in new[] { "Board", "DJBoard", "Mixer", "Menu", "TitleMenu", "World", "Rig", "OVRCameraRig" })
        { var g = GameObject.Find(n); if (g) s += $"\n  {n} active={g.activeInHierarchy} pos={g.transform.position}"; }
        foreach (var h in FindObjectsByType<OVRHand>(FindObjectsSortMode.None))
            s += $"\n  hand {h.name} tracked={h.IsTracked} conf={h.HandConfidence} pinch={h.GetFingerPinchStrength(OVRHand.HandFinger.Index):F2} aimValid={h.IsPointerPoseValid} aim={(h.PointerPose ? h.PointerPose.position.ToString() + h.PointerPose.forward : "null")}";
        foreach (var cv in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            if (cv.isRootCanvas) s += $"\n  canvas {cv.name} active={cv.gameObject.activeInHierarchy} mode={cv.renderMode} pos={cv.transform.position}";
        Debug.Log(s);
    }
}
#endif
