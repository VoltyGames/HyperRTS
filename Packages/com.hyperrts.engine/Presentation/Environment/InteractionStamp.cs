using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace HyperRTS.Presentation.Environment
{
    /// <summary>
    /// One quad drawn into an interaction map; matches <c>InteractionStamp</c> in InteractionBrush.shader. For the
    /// trample map Value is (grass flatten, crop damage, how much it sets the travel direction, round); for the track
    /// map (rut depth, churned soil, unused, round).
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct InteractionStamp
    {
        public const int Stride = 10 * sizeof(float);

        /// <summary>World XZ centre.</summary>
        public float2 Center;

        /// <summary>World XZ unit direction of travel; the quad's length runs along it.</summary>
        public float2 Forward;

        /// <summary>Half the quad's size in metres: x across, y along <see cref="Forward"/>.</summary>
        public float2 HalfSize;

        public float4 Value;
    }
}
