using UnityEngine;
using UnityEngine.XR;

namespace DJMaster
{
    // Built for seated play: puts the DJ booth (and the venue around it) right in front of wherever your head is,
    // at a comfortable reach. Everything stays inside a two-foot radius.
    public class SeatedAnchor : MonoBehaviour
    {
        public Transform head;
        [Tooltip("Where the board sits relative to the eyes: forward and down, in metres.")]
        public float forward = 0.40f;
        public float below = 0.42f;
        [Tooltip("Height of the board surface above the venue floor, in the world's local space.")]
        public float boardHeight = 0.82f;
        public float boardForward = 0.40f;

        float heightOffset;
        bool placed;
        float waited;

        void Start()
        {
            heightOffset = PlayerPrefs.GetFloat("boardHeight", 0f);
            if (OVRManager.display != null) OVRManager.display.RecenteredPose += Recenter;
            if (!XRSettings.isDeviceActive) Recenter();
        }

        void OnDestroy()
        {
            if (OVRManager.display != null) OVRManager.display.RecenteredPose -= Recenter;
        }

        void Update()
        {
            // Wait for real head tracking before the first placement.
            if (placed || !XRSettings.isDeviceActive) return;
            waited += Time.unscaledDeltaTime;
            if (waited > 0.5f && head.localPosition != Vector3.zero) Recenter();
        }

        public void Recenter()
        {
            Vector3 fwd = head.forward;
            fwd.y = 0;
            if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
            var yaw = Quaternion.LookRotation(fwd.normalized, Vector3.up);

            Vector3 boardWorld = head.position + yaw * Vector3.forward * forward + Vector3.down * (below - heightOffset);
            transform.rotation = yaw;
            transform.position = boardWorld - yaw * new Vector3(0, boardHeight, boardForward);
            placed = true;
        }

        public void AdjustHeight(float delta)
        {
            // Move by what the limit allows, not the full step, so the board stops at the limit instead of
            // drifting past it and jumping back on the next RECENTER.
            float next = Mathf.Clamp(heightOffset + delta, -0.2f, 0.2f);
            transform.position += Vector3.up * (next - heightOffset);
            heightOffset = next;
            PlayerPrefs.SetFloat("boardHeight", heightOffset);
            PlayerPrefs.Save();
        }
    }
}
