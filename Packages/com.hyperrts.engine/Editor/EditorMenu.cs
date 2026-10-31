namespace HyperRTS.Editor
{
    /// <summary>Paths and priorities of the HyperRTS menu, in the order the menu shows them.</summary>
    internal static class EditorMenu
    {
        public const string Root = "HyperRTS/";

        public const string Tools = Root + "Tools";
        public const int ToolsPriority = 0;

        public const string MapEditor = Root + "Map Editor";
        public const int MapEditorPriority = 1;

        public const string EffectPreview = Root + "Effect Preview";
        public const int EffectPreviewPriority = 2;

        public const string Documentation = Root + "Documentation/";
        public const int DocumentationPriority = 100;
    }
}
