using TMPro;
using UnityEngine;

namespace DJMaster
{
    // The floating panel above the board: title, instructions and a status line, plus a row of buttons.
    public class InstructionPanel : MonoBehaviour
    {
        public TextMeshPro title, body, status, feedback;
        public PokeButton[] actionButtons = new PokeButton[4];
        public TextMeshPro[] actionLabels = new TextMeshPro[4];
        public int wrapChars = 52;

        string lastBody, lastTitle, lastStatus, lastFeedback;
        bool lastColorSafe;

        public void Show(string titleText, string bodyText, string statusText = "", string feedbackText = "")
        {
            // Color-blind mode changes how the same text is drawn, so redraw everything when it flips.
            if (lastColorSafe != BoardColors.ColorSafe)
            {
                lastColorSafe = BoardColors.ColorSafe;
                lastTitle = lastBody = lastStatus = lastFeedback = null;
            }
            if (titleText != lastTitle) { title.text = BoardColors.Display(titleText); lastTitle = titleText; }
            if (bodyText != lastBody) { body.text = TextUtil.Wrap(BoardColors.Display(bodyText), wrapChars); lastBody = bodyText; }
            if (statusText != lastStatus) { status.text = TextUtil.Wrap(BoardColors.Display(statusText), wrapChars + 8); lastStatus = statusText; }
            if (feedbackText != lastFeedback) { feedback.text = BoardColors.Display(feedbackText); lastFeedback = feedbackText; }
        }

        // One label per button, left to right. Missing or null labels hide the button.
        public void SetActions(params string[] labels)
        {
            for (int i = 0; i < actionButtons.Length; i++)
            {
                bool on = i < labels.Length && labels[i] != null;
                actionButtons[i].gameObject.SetActive(on);
                if (on) actionLabels[i].text = labels[i];
            }
        }
    }
}
