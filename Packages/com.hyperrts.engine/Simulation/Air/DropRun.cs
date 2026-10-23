using Unity.Entities;
using Unity.Mathematics;
using Unity.Netcode;

namespace HyperRTS.Simulation.Air
{
    /// <summary>
    /// An aircraft's run over a drop point: <see cref="Active"/> replicates from <see cref="LeadSeconds"/> before the
    /// release until as long after, so every client can open its doors, and loads leave from <see cref="ReleasePoint"/>.
    /// </summary>
    public struct DropRun : IComponentData
    {
        /// <summary>Seconds of flight either side of the release that count as the run.</summary>
        public const float LeadSeconds = 3f;

        [GhostField] public bool Active;

        /// <summary>Where crates and bombs leave the aircraft, in its local space.</summary>
        public float3 ReleasePoint;

        /// <summary>Metres the release point sits aft of the origin, for an aircraft of <paramref name="scale"/>.</summary>
        public readonly float Behind(float scale) => -ReleasePoint.z * scale;
    }
}
