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
        public const int Rows = 5;
        public PokeButton[] buttons = new PokeButton[Rows];
        public TextMeshPro[] labels = new TextMeshPro[Rows];
        public TextMeshPro[] descriptions = new TextMeshPro[Rows];
        public int wrapChars = 40;
        [Tooltip("The skill level picker on the title screen, in SkillLevel order.")]
        public PokeButton[] levelButtons = new PokeButton[4];
        public GameObject levelRow;
        [Tooltip("Where the HOME and COLOR-BLIND tab sits while this panel is showing.")]
        public Transform corner;

        // The level picker only shows on the title screen. The picked level stays lit.
        public void ShowLevels(bool on)
        {
            if (levelRow == null) return;
            levelRow.SetActive(on);
            for (int i = 0; i < levelButtons.Length; i++)
                if (levelButtons[i] != null) levelButtons[i].Lit = (int)Skill.Current == i;
        }

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
