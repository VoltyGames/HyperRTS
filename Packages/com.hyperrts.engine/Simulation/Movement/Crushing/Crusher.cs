using HyperRTS.Simulation.Navigation.Grid;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Movement.Crushing
{
    /// <summary>A vehicle heavy enough to flatten <see cref="Destructible"/> scenery and crush enemy <see cref="Crushable"/> units it drives over.</summary>
    public struct Crusher : IComponentData
    {
        /// <summary>Metres per second it must pass to kill the units it drives over.</summary>
        public const float Pace = 1f;

        /// <summary>Compared with <see cref="Destructible.Crush"/> and <see cref="Crushable.Crush"/>: a size-3 tank crushes anything rated 1 to 3.</summary>
        public int Size;

        /// <summary>Only ground units crush; ships and aircraft never touch what is under them.</summary>
        public static bool CanCrush(NavLayer layer) => layer != NavLayer.Air && layer != NavLayer.Naval;

        /// <summary>Whether a velocity is fast enough to kill the units under the hull.</summary>
        public static bool IsCrushingPace(float2 velocity) => math.lengthsq(velocity) > Pace * Pace;

        /// <summary>
        /// Whether a ground vehicle with this crusher drives over an enemy foot unit of crush rating
        /// <paramref name="rating"/>; 0 is never.
        /// </summary>
        public readonly bool Crushes(NavLayer layer, bool isVehicle, int rating, bool victimIsVehicle, bool allied)
        {
            if (!CanCrush(layer) || !isVehicle)
            {
                return false;
            }

            if (victimIsVehicle || allied)
            {
                return false;
            }

            return rating > 0 && rating <= Size;
        }
    }
}
