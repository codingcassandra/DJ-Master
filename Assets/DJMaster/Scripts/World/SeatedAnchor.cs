using UnityEngine;
using UnityEngine.XR;

namespace DJMaster
{
    // Built for seated play: puts the DJ booth (and the venue around it) right in front of wherever your head is,
    // at a comfortable reach. Everything stays inside a two-foot radius.
    public class SeatedAnchor : MonoBehaviour
    {
        public Transform head;
        // Close enough that the far edge of the jog wheels is inside 0.61 m (two feet) of your eyes when you sit back
        [Tooltip("Where the board sits relative to the eyes: forward and down, in metres.")]
        public float forward = 0.32f;
        public float below = 0.36f;
        [Tooltip("Height of the board surface above the venue floor, in the world's local space.")]
        public float boardHeight = 0.806f;
        public float boardForward = 0.40f;
        [Tooltip("RECENTER keeps the board turned the way it is unless your head is turned more than this from it, in degrees.")]
        public float keepTurnWithin = 45f;
        public event System.Action Recentered; // DeskSnap listens to this so in MY ROOM the board goes on your real desk

        float heightOffset;
        bool placed;
        float waited;

        void Start()
        {
            heightOffset = PlayerPrefs.GetFloat("boardHeight", 0f);
            if (OVRManager.display != null) OVRManager.display.RecenteredPose += RecenterToHead;
            if (!XRSettings.isDeviceActive) RecenterToHead();
        }

        void OnDestroy()
        {
            if (OVRManager.display != null) OVRManager.display.RecenteredPose -= RecenterToHead;
        }

        void Update()
        {
            // Wait for real head tracking before the first placement.
            if (placed || !XRSettings.isDeviceActive) return;
            waited += Time.unscaledDeltaTime;
            if (waited > 0.5f && head.localPosition != Vector3.zero) RecenterToHead();
        }

        // The RECENTER button and venue changes. You look at the button when you poke it, so your head points
        // off to the side. If the board is still roughly in front of you we keep it turned the way it is and only
        // fix the distance and height, so it doesn't swing over toward the button
        public void Recenter()
        {
            Vector3 fwd = Flat(head.forward);
            Vector3 now = Flat(transform.forward);
            if (placed && Vector3.Angle(fwd, now) < keepTurnWithin) fwd = now;
            Place(fwd);
        }

        // The system recenter (hold the Meta button) and the first placement: you're looking straight ahead,
        // so the board goes right in front of where you look
        public void RecenterToHead() => Place(Flat(head.forward));

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0;
            return v.sqrMagnitude < 0.001f ? Vector3.forward : v.normalized;
        }

        void Place(Vector3 fwd)
        {
            var yaw = Quaternion.LookRotation(fwd, Vector3.up);

            Vector3 boardWorld = head.position + yaw * Vector3.forward * forward + Vector3.down * (below - heightOffset);
            transform.rotation = yaw;
            transform.position = boardWorld - yaw * new Vector3(0, boardHeight, boardForward);
            placed = true;
            Recentered?.Invoke();
        }

        public void AdjustHeight(float delta)
        {
            // Only move as far as the limit lets us, not the full step, so the board stops at the limit and doesn't
            // go past it and jump back on the next RECENTER
            float next = Mathf.Clamp(heightOffset + delta, -0.2f, 0.2f);
            transform.position += Vector3.up * (next - heightOffset);
            heightOffset = next;
            PlayerPrefs.SetFloat("boardHeight", heightOffset);
            PlayerPrefs.Save();
        }
    }
}
