using Unity.Mathematics;

namespace HyperRTS.Presentation.Destruction
{
    /// <summary>One settled debris piece kept in <see cref="LastingDebris"/>: where it lies, and when it began to bury.</summary>
    public struct LastingPiece
    {
        public float3 Position;
        public quaternion Rotation;
        public float3 Size;

        /// <summary>Metres it goes down to be fully under the ground.</summary>
        public float Depth;
        public bool Burying;

        /// <summary>The <see cref="LastingDebris"/> clock when it began to bury.</summary>
        public float BuryStart;

        public static LastingPiece Settled(in DebrisPiece piece) => new()
        {
            Position = piece.Position,
            Rotation = piece.Rotation,
            Size = piece.Size,
            Depth = piece.HalfHeight * 2f,
        };

        public readonly bool Buried(float clock) => Burying && clock - BuryStart >= LastingDebris.BuryTime;

        public readonly float4x4 Matrix(float clock)
        {
            var buried = Burying ? math.saturate((clock - BuryStart) / LastingDebris.BuryTime) : 0f;
            return float4x4.TRS(Position - math.up() * (Depth * buried), Rotation, Size);
        }
    }
}
