namespace DJMaster
{
    // One set of text sizes for the floating menus (start menu, DJ VOCAB), in the scene builder's character-size units
    // (local to each panel). Use them by role, not by eye, so every panel keeps the same proportions (#44).
    // The style guide (#32) can fold these in.
    public static class UIText
    {
        public const float Title = 0.0040f;    // the panel's name: DJ VOCAB, PICK YOUR LEVEL
        public const float Heading = 0.0017f;  // the line under the title, quiz feedback
        public const float Lead = 0.0019f;     // the one thing to read right now: a quiz question
        public const float Body = 0.0015f;     // descriptions, the word list
        public const float Small = 0.0013f;    // info lines, progress, footers
        public const float Button = 0.0017f;   // labels on menu buttons
        public const float Tab = 0.0012f;      // HOME, BACK, COLOR-BLIND, PLAYER: the small tabs above a panel

        // The tabs above a panel's top corners (HOME / COLOR-BLIND on the right, BACK or PLAYER on the left)
        public const float TabHeight = 0.036f, TabButtonHeight = 0.026f, TabButtonWidth = 0.064f;
    }
}
