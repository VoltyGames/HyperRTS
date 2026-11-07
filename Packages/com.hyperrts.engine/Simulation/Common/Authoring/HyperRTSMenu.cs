namespace HyperRTS.Simulation.Common.Authoring
{
    /// <summary>Add Component and Create Asset menu paths for authoring components.</summary>
    public static class HyperRTSMenu
    {
        public const string Root = "HyperRTS/";

        // Add Component: what the component is placed on, then its module and name.
        public const string Entity = Root + "Entity/";
        public const string Look = Root + "Look/";
        public const string Scene = Root + "Scene/";

        // Create Asset menu folders.
        public const string Animation = Root + "Animation/";
        public const string Audio = Root + "Audio/";
        public const string Combat = Root + "Combat/";
        public const string Destruction = Root + "Destruction/";
        public const string Effects = Root + "Effects/";
        public const string MapLibrary = Root + "Map Library/";
        public const string Match = Root + "Match/";
        public const string Resources = Root + "Resources/";
        public const string Rig = Root + "Rig/";
    }
}
