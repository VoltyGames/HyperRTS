using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine.Rendering;

namespace HyperRTS.Presentation.Environment.Ground.Stamps
{
    /// <summary>One quad drawn into a trample map. Read by TrampleBrush.shader through TrampleStamp.cs.hlsl.</summary>
    [StructLayout(LayoutKind.Sequential)]
    [GenerateHLSL(needAccessors: false)]
    public struct TrampleStamp
    {
        /// <summary>World XZ centre.</summary>
        public float2 Center;

        /// <summary>World XZ unit direction of travel, along the quad's length.</summary>
        public float2 Forward;

        /// <summary>Half the quad's size in metres: x across, y along <see cref="Forward"/>.</summary>
        public float2 HalfSize;

        /// <summary>Trample: (flatten, crop damage, direction weight, round). Tracks: (rut, soil, unused, round). Remains: (rubble, ash, fuel, wet mud).</summary>
        public float4 Value;

        /// <summary>Half the size of the world-axis box round the turned quad.</summary>
        public readonly float2 Extent => math.abs(new float2(Forward.y, -Forward.x)) * HalfSize.x + math.abs(Forward) * HalfSize.y;
    }
}
