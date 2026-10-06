using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Replays
{
    /// <summary>One entity's quantized state in a replay sample; quantized, idle entities compare equal.</summary>
    public struct ReplayEntity
    {
        /// <summary>Metres per position step; 16-bit coordinates cover ±2 km.</summary>
        public const float PositionStep = 1f / 16f;

        /// <summary><see cref="Progress"/> of an entity that isn't under construction.</summary>
        public const byte Finished = byte.MaxValue;

        private const float YawStep = 2f * math.PI / 65536f;

        /// <summary>Recording-local id, stable for the entity's lifetime.</summary>
        public int Key;
        public int TypeId;
        public short X;
        public short Y;
        public short Z;
        public ushort Yaw;
        public byte Faction;
        public byte Health;

        /// <summary>Construction progress in 0..254, or <see cref="Finished"/>.</summary>
        public byte Progress;

        public readonly float3 Position => new float3(X, Y, Z) * PositionStep;
        public readonly float HealthFraction => Health / 255f;
        public readonly bool UnderConstruction => Progress != Finished;
        public readonly float ConstructionValue => Progress / 254f;

        public static ReplayEntity Capture(int key, int typeId, byte faction, in LocalTransform transform,
            float healthFraction, float constructionProgress, bool underConstruction)
        {
            var cell = (int3)math.round(transform.Position / PositionStep);
            var forward = math.mul(transform.Rotation, math.forward());
            var yaw = math.atan2(forward.x, forward.z);
            return new ReplayEntity
            {
                Key = key,
                TypeId = typeId,
                X = (short)math.clamp(cell.x, short.MinValue, short.MaxValue),
                Y = (short)math.clamp(cell.y, short.MinValue, short.MaxValue),
                Z = (short)math.clamp(cell.z, short.MinValue, short.MaxValue),
                Yaw = (ushort)((int)math.round(yaw / YawStep) & 0xFFFF),
                Faction = faction,
                Health = (byte)math.round(math.saturate(healthFraction) * 255f),
                Progress = underConstruction ? (byte)math.round(math.saturate(constructionProgress) * 254f) : Finished,
            };
        }

        /// <summary>Transform between this sample and <paramref name="next"/>, turning the short way round.</summary>
        public readonly LocalTransform Interpolate(in ReplayEntity next, float t, float scale)
        {
            var turn = (short)(next.Yaw - Yaw);
            var yaw = (Yaw + turn * t) * YawStep;
            var position = math.lerp(Position, next.Position, t);
            return LocalTransform.FromPositionRotationScale(position, quaternion.RotateY(yaw), scale);
        }

        /// <summary>Whether nothing but the key differs, so the entity can be left out of a delta.</summary>
        public readonly bool SameState(in ReplayEntity other)
        {
            if (TypeId != other.TypeId || Faction != other.Faction)
            {
                return false;
            }

            if (X != other.X || Y != other.Y || Z != other.Z)
            {
                return false;
            }

            return Yaw == other.Yaw && Health == other.Health && Progress == other.Progress;
        }
    }
}
