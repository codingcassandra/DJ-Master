using TMPro;
using UnityEngine;

namespace DJMaster
{
    // The start screen: a purple menu floating in black, shown before you enter the booth.
    // Each row is a big button with a description beside it.
    public class StartMenu : MonoBehaviour
    {
        public static readonly Color Purple = new Color(0.66f, 0.3f, 1f);
        public static readonly Color Lavender = new Color(0.78f, 0.6f, 1f);

        public TextMeshPro title, subtitle, footer;
        public PokeButton[] buttons = new PokeButton[4];
        public TextMeshPro[] labels = new TextMeshPro[4];
        public TextMeshPro[] descriptions = new TextMeshPro[4];
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
                bool on = i < rows.Length && rows[i].label != null;
                buttons[i].Lit = false;
                buttons[i].gameObject.SetActive(on);
                descriptions[i].gameObject.SetActive(on);
                if (!on) continue;
                labels[i].text = rows[i].label;
                descriptions[i].text = TextUtil.Wrap(rows[i].description ?? "", wrapChars);
            }
        }
    }
}
