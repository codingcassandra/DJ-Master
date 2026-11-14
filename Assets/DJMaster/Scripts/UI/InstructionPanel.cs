using TMPro;
using UnityEngine;

namespace DJMaster
{
    // The floating panel above the board: title, instructions and a status line, plus a row of buttons
    // on the board's front ledge.
    public class InstructionPanel : MonoBehaviour
    {
        public TextMeshPro title, body, status, feedback;
        public PokeButton[] actionButtons = new PokeButton[4];
        public TextMeshPro[] actionLabels = new TextMeshPro[4];
        public int wrapChars = 52;
        public int statusWrapChars = 60;
        [Tooltip("The body grows up from where it sits and the title and status ride on top of it, so a short step stays close to the board.")]
        public bool stackTitle;
        public float titleGap = 0.008f;

        string lastBody, lastTitle, lastStatus, lastFeedback;
        bool lastColorSafe;

        public void Show(string titleText, string bodyText, string statusText = "", string feedbackText = "")
        {
            // Color-blind mode changes how the text looks, so we redraw everything when it's turned on or off
            if (lastColorSafe != BoardColors.ColorSafe)
            {
                lastColorSafe = BoardColors.ColorSafe;
                lastTitle = lastBody = lastStatus = lastFeedback = null;
            }
            if (titleText != lastTitle) { title.text = BoardColors.Display(titleText); lastTitle = titleText; }
            if (bodyText != lastBody)
            {
                body.text = TextUtil.Wrap(BoardColors.Display(bodyText), wrapChars);
                lastBody = bodyText;
                if (stackTitle) StackTitle();
            }
            if (statusText != lastStatus) { status.text = TextUtil.Wrap(BoardColors.Display(statusText), statusWrapChars); lastStatus = statusText; }
            if (feedbackText != lastFeedback) { feedback.text = BoardColors.Display(feedbackText); lastFeedback = feedbackText; }
        }

        // Puts the title and status right on top of the body, however many lines it has
        void StackTitle()
        {
            float top = body.transform.localPosition.y + (body.text.Length > 0 ? body.preferredHeight : 0f) + titleGap;
            var t = title.transform.localPosition;
            title.transform.localPosition = new Vector3(t.x, top, t.z);
            var s = status.transform.localPosition;
            status.transform.localPosition = new Vector3(s.x, top, s.z);
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
