namespace HyperRTS.Editor
{
    /// <summary>Paths and priorities of the HyperRTS menu, in the order the menu shows them.</summary>
    internal static class EditorMenu
    {
        /// <summary>Unity draws a separator between items whose priorities differ by more than 10.</summary>
        public const int SeparatorGap = 11;

        public const string Root = "HyperRTS/";

        public const string CreateScene = Root + "Create RTS Scene...";
        public const int CreateScenePriority = 0;

        public const string GenerateIcons = Root + "Generate Component Icons";
        public const int GenerateIconsPriority = 19;

        public const string Validate = Root + "Validate";
        public const int ValidatePriority = 20;

        public const string Catalog = Root + "Catalog";
        public const int CatalogPriority = 21;

        public const string Cheats = Root + "Cheats";
        public const int CheatsPriority = 22;

        public const string Network = Root + "Network/";
        public const int NetworkPriority = 30;

        public const string Replays = Root + "Replays/";
        public const int ReplaysPriority = 50;

        public const string Documentation = Root + "Documentation/";
        public const int DocumentationPriority = 100;
    }
}
