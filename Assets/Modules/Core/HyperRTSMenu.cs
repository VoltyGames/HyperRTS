namespace HyperRTS.Core
{
    /// <summary>Add Component menu paths for authoring components.</summary>
    public static class HyperRTSMenu
    {
        public const string Root = "HyperRTS/";
        public const string Attack = Root + "Attack/";
        public const string Buildings = Root + "Buildings/";
        public const string Cameras = Root + "Cameras/";
        public const string Health = Root + "Health/";
        public const string Resources = Root + "Resources/";
        public const string Selection = Root + "Selection/";
        public const string Units = Root + "Units/";
    }

    /// <summary>Editor icon paths per module (HyperRTS ▸ Tools ▸ Generate Component Icons).</summary>
    public static class HyperRTSIcons
    {
        private const string Dir = "Assets/Modules/Editor/Icons/";
        public const string Attack = Dir + "Attack.png";
        public const string Buildings = Dir + "Buildings.png";
        public const string Cameras = Dir + "Cameras.png";
        public const string Health = Dir + "Health.png";
        public const string Resources = Dir + "Resources.png";
        public const string Selection = Dir + "Selection.png";
        public const string Units = Dir + "Units.png";
    }

    /// <summary>Doc URLs for <c>[HelpURL]</c>.</summary>
    public static class HyperRTSDocs
    {
        public const string Root = "https://github.com/SIGamesStudio/HyperRTS/blob/main/docs/";
        public const string WorldSetup = Root + "world-setup.md";
        public const string Roadmap = Root + "roadmap.md";
    }
}
