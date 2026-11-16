using UnityEngine;

namespace DJMaster
{
    // The look in one place (#32): colors, the font, text sizes and the panel settings. SceneBuilder and the panels read
    // these instead of their own numbers, so a change here changes the whole game.
    //
    // "Midnight Chrome": graphite body, brushed metal, our purple as the brand color, one cyan for "touch this",
    // light coming from the gear itself, dark glass panels, one clean font (Inter, SIL Open Font License).
    // The board's color code (Deck A blue, Deck B pink, PLAY green ...) and color-blind mode stay in BoardColors.
    public static class Style
    {
        // ---- Colors ----
        public static readonly Color Graphite = new(0.06f, 0.06f, 0.07f);     // the board body
        public static readonly Color Metal = new(0.7f, 0.71f, 0.75f);         // trims and fader caps
        public static readonly Color Purple = new(0.66f, 0.3f, 1f);           // brand: titles, panel edges, menu buttons
        public static readonly Color Lavender = new(0.78f, 0.6f, 1f);         // text on the purple menus
        public static readonly Color Cyan = new(0.2f, 0.85f, 1f);             // "touch this": the lesson glow, the selected row
        public static readonly Color TextWhite = Color.white;
        public static readonly Color TextGrey = new(0.75f, 0.78f, 0.85f);     // labels on the board and panels
        public static readonly Color HeadingColor = new(1f, 0.85f, 0.3f);          // the instruction panel's titles
        public static readonly Color Good = new(0.3f, 1f, 0.5f);              // feedback and status that went well
        public static readonly Color Screen = new(0.015f, 0.02f, 0.03f);      // the board's screens
        public static readonly Color Rule = new(0.08f, 0.08f, 0.1f);          // thin lines on screens

        // ---- Font ----
        public const string FontPath = "Assets/DJMaster/Fonts/Inter SDF.asset";

        // ---- Text sizes, by role (scene builder character-size units, local to each panel) ----
        public const float Title = 0.0040f;    // a menu's name: DJ VOCAB, PICK YOUR LEVEL
        public const float PanelTitle = 0.0021f; // the instruction panel's and set builder's title line
        public const float Heading = 0.0017f;  // the line under a title, quiz feedback
        public const float Lead = 0.0019f;     // the one thing to read right now: a quiz question, panel feedback
        public const float Body = 0.0015f;     // descriptions, the word list, the instruction panel's text
        public const float Small = 0.0013f;    // info lines, progress, footers
        public const float Button = 0.0017f;   // labels on menu buttons
        public const float Tab = 0.0012f;      // HOME, BACK, COLOR-BLIND, PLAYER: the small tabs above a panel

        // ---- Panels ----
        public static readonly Color PanelTint = new(0.03f, 0.008f, 0.06f, 0.5f); // dark glass
        public static readonly Color PanelRim = new(0.66f, 0.3f, 1f, 0.35f);       // its thin purple rim
        public const float PanelSheen = 0.12f;
        public static readonly Color RowTint = new(0.3f, 0.1f, 0.5f, 0.42f);       // menu rows: see-through until lit
        public static readonly Color RowRim = new(0.85f, 0.7f, 1f, 0.9f);
        public static readonly Color RowLit = new(0.62f, 0.28f, 1f, 0.92f);
        public const float RowSheen = 0.3f;
        public const float EdgeWidth = 0.004f;                                      // the bright strip along a panel's top
        public static readonly Color TabBack = new(0.06f, 0.06f, 0.09f);            // the tabs above a panel
        public static readonly Color TabButton = new(0.2f, 0.2f, 0.28f);
        public const float TabHeight = 0.036f, TabButtonHeight = 0.026f, TabButtonWidth = 0.064f;
    }
}
