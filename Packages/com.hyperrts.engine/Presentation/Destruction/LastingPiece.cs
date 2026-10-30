using Unity.Mathematics;

namespace HyperRTS.Presentation.Destruction
{
    /// <summary>One settled debris piece kept in <see cref="LastingDebris"/>: where it lies, and when it began to sink.</summary>
    public struct LastingPiece
    {
        public float3 Position;
        public quaternion Rotation;
        public float3 Size;

        /// <summary>Metres it sinks to be fully under the ground.</summary>
        public float Depth;
        public bool Sinking;

        /// <summary>The <see cref="LastingDebris"/> clock when it began to sink.</summary>
        public float SinkStart;

        public static LastingPiece Settled(in DebrisPiece piece) => new()
        {
            Position = piece.Position,
            Rotation = piece.Rotation,
            Size = piece.Size,
            Depth = piece.HalfHeight * 2f,
        };

        public readonly bool Sunk(float clock) => Sinking && clock - SinkStart >= LastingDebris.SinkTime;

        public readonly float4x4 Matrix(float clock)
        {
            var sunk = Sinking ? math.saturate((clock - SinkStart) / LastingDebris.SinkTime) : 0f;
            return float4x4.TRS(Position - math.up() * (Depth * sunk), Rotation, Size);
        }
    }
}
