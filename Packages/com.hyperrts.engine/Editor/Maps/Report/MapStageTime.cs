using System;

namespace HyperRTS.Editor.Maps.Report
{
    /// <summary>How long one stage of a map build took, as the report lists it.</summary>
    [Serializable]
    public sealed class MapStageTime
    {
        public string stage;
        public long milliseconds;
    }
}
