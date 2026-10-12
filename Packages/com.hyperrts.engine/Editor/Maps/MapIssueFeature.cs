using System;

namespace HyperRTS.Editor.Maps
{
    /// <summary>
    /// The spec feature an issue is about: the spec array (<c>roads</c>, <c>spawns</c>...) and the index in it. Empty
    /// <see cref="array"/> when the issue is about the map as a whole.
    /// </summary>
    [Serializable]
    public struct MapIssueFeature
    {
        public string array;
        public int index;

        public MapIssueFeature(string array, int index)
        {
            this.array = array;
            this.index = index;
        }

        public static MapIssueFeature None => default;

        public bool IsNone => string.IsNullOrEmpty(array);

        public override string ToString() => IsNone ? "map" : $"{array}[{index}]";
    }
}
