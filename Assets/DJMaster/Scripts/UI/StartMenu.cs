using TMPro;
using UnityEngine;

namespace DJMaster
{
    // The start screens: a purple menu floating in black, shown before you enter the booth.
    // The first screen is just big buttons down the middle. The next ones (level, venue, lesson) put a
    // description beside each button.
    public class StartMenu : MonoBehaviour
    {
        public static readonly Color Purple = new Color(0.66f, 0.3f, 1f);
        public static readonly Color Lavender = new Color(0.78f, 0.6f, 1f);

        public TextMeshPro title, subtitle, footer;
        public const int Rows = 5;
        public PokeButton[] buttons = new PokeButton[Rows];
        public TextMeshPro[] labels = new TextMeshPro[Rows];
        public TextMeshPro[] descriptions = new TextMeshPro[Rows];
        public int wrapChars = 40;
        [Tooltip("Where the HOME and COLOR-BLIND tab sits while this panel is showing.")]
        public Transform corner;
        [Tooltip("The DJ MASTER wordmark, shown in place of the title on the first screen.")]
        public GameObject wordmark;

        [Header("Buttons-only layout (the first screen)")]
        [Tooltip("Row centers on the buttons-only screen, top to bottom.")]
        public float centeredTop = 0.073f, centeredStep = 0.056f;
        [Tooltip("How much bigger the buttons are when they're alone in the middle.")]
        public float centeredScale = 1.3f;

        Vector3[] rowPos;

        void Awake() => Remember();

        void Remember()
        {
            if (rowPos != null) return;
            rowPos = new Vector3[buttons.Length];
            for (int i = 0; i < buttons.Length; i++) rowPos[i] = buttons[i].transform.localPosition;
        }

        public bool Centered { get; private set; }

        public void Show(string titleText, string subtitleText, string footerText)
        {
            if (title.text != titleText) title.text = titleText;
            if (subtitle.text != subtitleText) subtitle.text = subtitleText;
            if (footer.text != footerText) footer.text = footerText;
        }

        // Just the buttons, centered and bigger. Missing rows are hidden.
        public void SetButtons(params string[] rows)
        {
            var r = new (string, string)[rows.Length];
            for (int i = 0; i < rows.Length; i++) r[i] = (rows[i], null);
            Layout(true, r);
        }

        // One row per (label, description). Missing rows are hidden.
        public void SetRows(params (string label, string description)[] rows) => Layout(false, rows);

        void Layout(bool centered, (string label, string description)[] rows)
        {
            Remember();
            Centered = centered;
            if (wordmark != null) wordmark.SetActive(centered);
            for (int i = 0; i < buttons.Length; i++)
            {
                var b = buttons[i];
                b.Lit = false; // a row lit on one screen shouldn't stay lit on the next
                bool on = i < rows.Length && rows[i].label != null;
                b.gameObject.SetActive(on);
                descriptions[i].gameObject.SetActive(on && !centered);
                b.transform.localPosition = centered ? new Vector3(0, centeredTop - i * centeredStep, rowPos[i].z) : rowPos[i];
                b.transform.localScale = Vector3.one * (centered ? centeredScale : 1f);
                if (!on) continue;
                labels[i].text = rows[i].label;
                descriptions[i].text = centered ? "" : TextUtil.Wrap(rows[i].description ?? "", wrapChars);
            }
        }
    }
}
