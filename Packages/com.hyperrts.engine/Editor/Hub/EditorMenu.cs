namespace HyperRTS.Editor.Hub
{
    /// <summary>
    /// Paths and priorities of the HyperRTS menu, in the order the menu shows them. The hotkeys are defaults; rebind
    /// them under Main Menu in Edit ▸ Shortcuts.
    /// </summary>
    public static class EditorMenu
    {
        public const string Root = "HyperRTS/";

        public const string Tools = Root + "Tools %#t";
        public const int ToolsPriority = 0;

        public const string MapEditor = Root + "Map Editor %#m";
        public const int MapEditorPriority = 1;

        public const string Documentation = Root + "Documentation";
        public const int DocumentationPriority = 100;
    }
}
