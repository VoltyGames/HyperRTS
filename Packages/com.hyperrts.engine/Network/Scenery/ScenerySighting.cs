namespace HyperRTS.Network.Scenery
{
    /// <summary>A scenery change one team has just seen, for its clients.</summary>
    public struct ScenerySighting
    {
        public byte Team;

        /// <summary>The team saw it happen, rather than finding the result later.</summary>
        public bool Witnessed;

        public SceneryChange Change;
    }
}
