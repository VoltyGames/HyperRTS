using System.Collections.Generic;

namespace HyperRTS.Presentation.Environment
{
    /// <summary>One frame's stamps for the two interaction maps, gathered by <see cref="InteractionStampSystem"/>.</summary>
    public sealed class InteractionStamps
    {
        public readonly List<InteractionStamp> Trample = new();
        public readonly List<InteractionStamp> Tracks = new();

        public void Clear()
        {
            Trample.Clear();
            Tracks.Clear();
        }
    }
}
