using TMPro;
using UnityEngine;

namespace DJMaster
{
    // HOME and COLOR-BLIND, always in the same spot: a little tab on the top right corner of whatever panel is
    // showing (the instruction panel, the set builder, the start menu or DJ VOCAB). There's only one set of
    // buttons, GameDirector moves it onto the panel that's showing, so it looks and works the same everywhere.
    public class SystemButtons : MonoBehaviour
    {
        // The tab buttons' resting color, shared with the BACK and PLAYER tabs so every tab looks the same
        public static readonly Color OffColor = new Color(0.2f, 0.2f, 0.28f);

        public PokeButton home, colorBlind;
        public TextMeshPro colorBlindLabel;
        [Tooltip("The dark backing. It's right-aligned on the corner, so it shrinks to fit when HOME is hidden.")]
        public Transform tab;
        public float fullWidth = 0.215f, shortWidth = 0.14f;

        void OnEnable()
        {
            BoardColors.Changed += Refresh;
            Refresh();
        }

        void OnDisable() => BoardColors.Changed -= Refresh;

        // Puts the tab on a panel's corner. On the start menu you're already home, so HOME hides there.
        public void MountOn(Transform corner, bool showHome)
        {
            if (corner == null) { gameObject.SetActive(false); return; }
            transform.SetParent(corner, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            home.gameObject.SetActive(showHome);
            if (tab != null)
            {
                float w = showHome ? fullWidth : shortWidth;
                tab.localScale = new Vector3(w, tab.localScale.y, tab.localScale.z);
                tab.localPosition = new Vector3(-w / 2, tab.localPosition.y, tab.localPosition.z);
            }
            gameObject.SetActive(true);
            Refresh();
        }

        public void Refresh()
        {
            colorBlind.Lit = BoardColors.ColorSafe;
            // Say it in words too, so you don't have to tell lit from unlit by color
            if (colorBlindLabel != null) colorBlindLabel.text = BoardColors.ColorSafe ? "COLOR-BLIND: ON" : "COLOR-BLIND: OFF";
        }
    }
}
