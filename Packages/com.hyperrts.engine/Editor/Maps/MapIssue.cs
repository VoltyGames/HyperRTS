namespace HyperRTS.Editor.Maps
{
    /// <summary>One error or warning and the spec feature it is about.</summary>
    public readonly struct MapIssue
    {
        public MapIssue(string message, MapIssueFeature feature)
        {
            Message = message;
            Feature = feature;
        }

        public string Message { get; }

        /// <summary><see cref="MapIssueFeature.None"/> when the issue is about the map as a whole.</summary>
        public MapIssueFeature Feature { get; }

        public override string ToString() => Message;
    }
}
