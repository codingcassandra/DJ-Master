using UnityEngine;

namespace DJMaster
{
    // The start screen: a purple menu floating in black, shown before you enter the booth.
    // Each row is a big button with a description beside it.
    public class StartMenu : MonoBehaviour
    {
        public static readonly Color Purple = new Color(0.66f, 0.3f, 1f);
        public static readonly Color Lavender = new Color(0.78f, 0.6f, 1f);

        public TextMesh title, subtitle, footer;
        public PokeButton[] buttons = new PokeButton[4];
        public TextMesh[] labels = new TextMesh[4];
        public TextMesh[] descriptions = new TextMesh[4];
        public int wrapChars = 40;

        public void Show(string titleText, string subtitleText, string footerText)
        {
            if (title.text != titleText) title.text = titleText;
            if (subtitle.text != subtitleText) subtitle.text = subtitleText;
            if (footer.text != footerText) footer.text = footerText;
        }

        // One row per (label, description). Missing rows are hidden.
        public void SetRows(params (string label, string description)[] rows)
        {
            for (int i = 0; i < buttons.Length; i++)
            {
                buttons[i].Lit = false; // a row lit on one screen (like COLOR-BLIND) shouldn't stay lit on the next
                bool on = i < rows.Length && rows[i].label != null;
                buttons[i].gameObject.SetActive(on);
                descriptions[i].gameObject.SetActive(on);
                if (!on) continue;
                labels[i].text = rows[i].label;
                descriptions[i].text = TextUtil.Wrap(rows[i].description ?? "", wrapChars);
            }
        }
    }
}
