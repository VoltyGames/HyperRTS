using HyperRTS.Simulation.Orders;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Interaction
{
    /// <summary>Pure helpers that turn a world click into a <see cref="PlayerCommand"/>.</summary>
    public static class CommandMath
    {
        /// <summary>Where a ray meets the horizontal plane at <paramref name="height"/>; false when it never does.</summary>
        public static bool TryGroundPoint(float3 origin, float3 direction, float height, out float3 point)
        {
            point = default;
            if (direction.y > -1e-5f && direction.y < 1e-5f)
            {
                return false;
            }

            var distance = (height - origin.y) / direction.y;
            if (distance < 0f)
            {
                return false;
            }

            point = origin + direction * distance;
            point.y = height;
            return true;
        }

        /// <summary>Attack-move onto a hostile becomes a direct attack on it.</summary>
        public static CommandType ResolveTargeted(CommandType pending, bool targetIsHostile) =>
            pending == CommandType.AttackMove && targetIsHostile ? CommandType.Attack : pending;
    }
}
