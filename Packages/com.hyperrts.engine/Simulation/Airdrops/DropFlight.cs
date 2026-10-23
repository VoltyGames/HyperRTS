using HyperRTS.Simulation.Air;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Airdrops
{
    /// <summary>
    /// An aircraft flying its <see cref="Route"/> over the drop point and off the map; the delivery or strike it
    /// carries is a component beside this one.
    /// </summary>
    public struct DropFlight : IComponentData
    {
        public DropRoute Route;

        /// <summary>Set once it dropped or gave up; it then flies off the map and despawns.</summary>
        public bool Leaving;

        /// <summary>Metres from the release point to the drop point, as of the last <see cref="Fly"/>.</summary>
        public float DistanceToDrop;

        public readonly bool HasLeft(in MapSettings map, float3 position) => Leaving && !map.Contains(position);

        /// <summary>Within <paramref name="seconds"/> of flight of the drop point, before or after it.</summary>
        public readonly bool IsWithin(float seconds, float speed) => DistanceToDrop <= seconds * speed;

        /// <summary>
        /// Steers one frame and tracks the <paramref name="run"/>; true on the frame the release point comes over the
        /// drop point, when the load should leave from <paramref name="release"/>.
        /// </summary>
        public bool Fly(in LocalTransform transform, float speed, ref DropRun run, ref MoveDestination destination,
            out float3 release)
        {
            release = transform.TransformPoint(run.ReleasePoint);
            DistanceToDrop = Route.DistanceToTarget(release);
            run.Active = IsWithin(DropRun.LeadSeconds, speed);
            var released = !Leaving && Route.IsReached(release);
            Leaving |= released;
            destination.Value = Route.Destination(Leaving, run.Behind(transform.Scale));
            return released;
        }
    }
}
