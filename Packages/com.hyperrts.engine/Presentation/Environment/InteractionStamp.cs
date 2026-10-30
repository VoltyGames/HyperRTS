using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace HyperRTS.Presentation.Environment
{
    /// <summary>One quad drawn into an interaction map. Matches <c>InteractionStamp</c> in InteractionBrush.shader.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct InteractionStamp
    {
        public const int Stride = 10 * sizeof(float);

        /// <summary>World XZ centre.</summary>
        public float2 Center;

        /// <summary>World XZ unit direction of travel, along the quad's length.</summary>
        public float2 Forward;

        /// <summary>Half the quad's size in metres: x across, y along <see cref="Forward"/>.</summary>
        public float2 HalfSize;

        /// <summary>Trample: (flatten, crop damage, direction weight, round). Tracks: (rut, soil, unused, round). Remains: (rubble, ash, fuel, wet mud).</summary>
        public float4 Value;
    }
}
