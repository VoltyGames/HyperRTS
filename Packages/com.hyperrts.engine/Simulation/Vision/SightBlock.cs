using HyperRTS.Simulation.Navigation.Areas;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Vision
{
    /// <summary>
    /// A box, turned with its entity, that blocks sight: solid below <see cref="Height"/> (a house, a wall, a wreck),
    /// porous below <see cref="PorousTop"/> (a tree crown, smoke), or both. Disabled once it stops blocking, as when a
    /// wreck is buried.
    /// </summary>
    public struct SightBlock : IComponentData, IEnableableComponent
    {
        /// <summary>Ground footprint (X by Z), centred on the entity.</summary>
        public float2 Size;

        /// <summary>Solid top above the entity's position; 0 for cover that sight seeps through.</summary>
        public float Height;

        /// <summary>Share of sight lost per metre through it below <see cref="PorousTop"/>; 0.1 is lost after 10 m.</summary>
        public float Density;

        /// <summary>Top of the porous cover above the entity's position.</summary>
        public float PorousTop;

        /// <summary>Still infantry on it are concealed, as in woods, ruins and trenches.</summary>
        public bool Conceals;

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
