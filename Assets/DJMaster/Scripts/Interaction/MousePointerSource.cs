using UnityEngine;
using UnityEngine.XR;

namespace DJMaster
{
    // Editor and desktop fallback: the mouse acts as a fingertip so the whole game can be tested without a headset.
    //   Left click/drag  touch a control     Scroll  turn the knob under the cursor, or zoom anywhere else
    //   Right drag       look around
    [DefaultExecutionOrder(-100)]
    public class MousePointerSource : MonoBehaviour
    {
        public Camera cam;
        public Transform lookPivot; // rotated by right-drag
        [Tooltip("Without a headset the camera sits at the rig origin, so lift it to seated eye height.")]
        public Transform rigRoot;
        public float editorEyeHeight = 1.24f;
        [Tooltip("Start looking down at the board rather than at the wall.")]
        public float editorPitch = 34f;
        public float editorFov = 58f;
        public Vector2 zoomRange = new Vector2(30f, 80f);

        Pointer pointer;
        Plane dragPlane;
        Vector3 dragNormal;
        bool dragging;
        float yaw, pitch;

        bool XRActive => XRSettings.isDeviceActive;

        void OnEnable() => pointer = Pointers.Create();
        void OnDisable() { Pointers.Remove(pointer); pointer = null; }

        void Start()
        {
            if (XRActive) return;
            if (rigRoot != null) rigRoot.position += Vector3.up * editorEyeHeight;
            if (cam != null) cam.fieldOfView = editorFov;
            if (lookPivot != null)
            {
                yaw = lookPivot.localEulerAngles.y;
                pitch = editorPitch;
                lookPivot.localRotation = Quaternion.Euler(pitch, yaw, 0);
            }
        }

        void LateUpdate()
        {
            if (XRActive || cam == null) { pointer.Active = false; return; }

            if (Input.GetMouseButton(1) && lookPivot != null)
            {
                yaw += Input.GetAxis("Mouse X") * 3f;
                pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * 3f, -80f, 80f);
                lookPivot.localRotation = Quaternion.Euler(pitch, yaw, 0);
            }

            var ray = cam.ScreenPointToRay(Input.mousePosition);

            float scroll = Input.mouseScrollDelta.y;
            if (scroll != 0)
            {
                if (Physics.Raycast(ray, out var wheelHit, 3f) && wheelHit.collider.GetComponentInParent<Knob>() is { } knob)
                    knob.Nudge(scroll * 0.05f);
                else // zoom, so small text can be read in a small Game view
                    cam.fieldOfView = Mathf.Clamp(cam.fieldOfView - scroll * 3f, zoomRange.x, zoomRange.y);
            }

            if (Input.GetMouseButtonDown(0) && Physics.Raycast(ray, out var hit, 3f))
            {
                dragging = true;
                dragNormal = hit.normal;
                dragPlane = new Plane(hit.normal, hit.point);
            }
            if (!Input.GetMouseButton(0)) dragging = false;

            pointer.DirectPress = true;
            pointer.Active = dragging;
            if (dragging && dragPlane.Raycast(ray, out float dist))
                pointer.Position = ray.GetPoint(dist) - dragNormal * 0.009f; // just under the surface, like a pressing finger
        }
    }
}
