using HyperRTS.Simulation.Navigation.Areas;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Vision
{
    /// <summary>
    /// A box, turned with its entity, that sight can't pass below its top, such as a house, a wall or a wreck.
    /// Disabled once it stops blocking, as when a wreck is buried.
    /// </summary>
    public struct SightBlock : IComponentData, IEnableableComponent
    {
        /// <summary>Ground footprint (X by Z), centred on the entity.</summary>
        public float2 Size;

        /// <summary>Top above the entity's position.</summary>
        public float Height;

        public readonly NavBox Box(in LocalTransform transform) => new(transform.Position, transform.Rotation, Size);

        public readonly float Top(in LocalTransform transform) => transform.Position.y + Height;

        /// <summary>A hash of the block stamped at <paramref name="transform"/>, to tell when it changed.</summary>
        public readonly ulong Hash(in LocalTransform transform) => NavBox.Hash(transform, this);
    }

    /// <summary>
    /// On a container with a <see cref="SightBlock"/>, such as a garrisoned house: passengers inside see from this far
    /// above its top, and its own walls don't hide anything from them.
    /// </summary>
    public struct PassengerEye : IComponentData
    {
        public float AboveTop;
    }
}
