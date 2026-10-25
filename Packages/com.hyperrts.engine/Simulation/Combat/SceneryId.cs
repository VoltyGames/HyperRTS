using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>
    /// Name of a piece of map scenery that every world loading the map agrees on, so multiplayer can refer to it
    /// without making it a ghost: a hash of where it stands (to the centimetre) and how it is turned. Its remains
    /// carry its id too, so a clearing can name them.
    /// </summary>
    public struct SceneryId : IComponentData
    {
        private const ulong Basis = 14695981039346656037ul;
        private const ulong Prime = 1099511628211ul;

        /// <summary>Never 0, which stands for "no scenery" in messages.</summary>
        public ulong Value;

        public static SceneryId At(float3 position, quaternion rotation)
        {
            var cell = (int3)math.round(position * 100f);
            var turn = (int4)math.round(rotation.value * 10000f);
            var hash = Mix(Mix(Mix(Basis, cell.x), cell.y), cell.z);
            hash = Mix(Mix(Mix(Mix(hash, turn.x), turn.y), turn.z), turn.w);
            return new SceneryId { Value = hash == 0 ? 1 : hash };
        }

        /// <summary>FNV-1a over the value's four bytes.</summary>
        private static ulong Mix(ulong hash, int value)
        {
            for (var shift = 0; shift < 32; shift += 8)
            {
                hash = (hash ^ (byte)(value >> shift)) * Prime;
            }

            return hash;
        }
    }
}
