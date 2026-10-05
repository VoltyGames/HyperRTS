namespace HyperRTS.Core
{
    /// <summary>Add Component and Create Asset menu paths for authoring components.</summary>
    public static class HyperRTSMenu
    {
        public const string Root = "HyperRTS/";
        public const string Buildings = Root + "Buildings/";
        public const string Cameras = Root + "Cameras/";
        public const string Combat = Root + "Combat/";
        public const string Match = Root + "Match/";
        public const string Navigation = Root + "Navigation/";
        public const string Resources = Root + "Resources/";
        public const string Selection = Root + "Selection/";
        public const string UI = Root + "UI/";
        public const string Units = Root + "Units/";
        public const string Vision = Root + "Vision/";
    }

    /// <summary>Editor icon paths per module (HyperRTS ▸ Generate Component Icons).</summary>
    public static class HyperRTSIcons
    {
        private const string Dir = "Assets/Modules/Editor/Icons/";
        public const string Buildings = Dir + "Buildings.png";
        public const string Cameras = Dir + "Cameras.png";
        public const string Combat = Dir + "Combat.png";
        public const string Match = Dir + "Match.png";
        public const string Navigation = Dir + "Navigation.png";
        public const string Resources = Dir + "Resources.png";
        public const string Selection = Dir + "Selection.png";
        public const string UI = Dir + "UI.png";
        public const string Units = Dir + "Units.png";
        public const string Vision = Dir + "Vision.png";
    }

    /// <summary>Doc URLs for <c>[HelpURL]</c>.</summary>
    public static class HyperRTSDocs
    {
        public const string Root = "https://github.com/SIGamesStudio/HyperRTS/blob/main/docs/";
        public const string GettingStarted = Root + "getting-started.md";
        public const string Modules = Root + "modules.md";
        public const string WorldSetup = Root + "world-setup.md";
        public const string Roadmap = Root + "roadmap.md";
    }
}
