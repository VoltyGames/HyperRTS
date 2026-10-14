using Unity.Entities;
using Unity.NetCode;

namespace HyperRTS.Simulation.Capture
{
    /// <summary>A building other players can take over.</summary>
    public struct Capturable : IComponentData
    {
        public float CaptureTime;
    }

    /// <summary>Captures <see cref="Capturable"/> buildings through Capture orders.</summary>
    public struct Capturer : IComponentData
    {
        public float Rate;
        public bool ConsumedOnCapture;
    }

    /// <summary>Capture under way: who is capturing and how far (0..1); resets when another player starts.</summary>
    public struct CaptureProgress : IComponentData
    {
        [GhostField] public byte Faction;
        [GhostField(Quantization = 1000)] public float Value;

        /// <summary>Someone other than <paramref name="owner"/> has started taking the building.</summary>
        public readonly bool IsUnderWay(byte owner)
        {
            if (Faction == Common.Faction.Neutral || Faction == owner)
            {
                return false;
            }

            return Value > 0f;
        }
    }
}
