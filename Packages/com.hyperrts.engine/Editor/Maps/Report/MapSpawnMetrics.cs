using System;

namespace HyperRTS.Editor.Maps.Report
{
    /// <summary>One base's numbers: its nearest enemy, its buildable ground and the resources close to it.</summary>
    [Serializable]
    public sealed class MapSpawnMetrics
    {
        public int slot;
        public int team;

        /// <summary>Seconds to the nearest enemy base at the rules' vehicle speed.</summary>
        public float rush;

        /// <summary>Share of the base radius units can drive and build on.</summary>
        public float buildable;

        /// <summary>Finite resource nodes within reach of the base and their total amount.</summary>
        public int finiteFields;
        public int finiteSupply;

        /// <summary>Regrowing resource nodes within reach of the base (an error: main and natural fields are finite).</summary>
        public int renewableFields;
        public int renewableSupply;
    }
}
