using System;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>
    /// Who a unit is to the map: the layer it moves on, the scenery it drives through and its exact radius. Units
    /// sharing all three share access and regions.
    /// </summary>
    public readonly struct MovementClass : IEquatable<MovementClass>
    {
        public readonly NavLayer Layer;

        /// <summary>Highest scenery crush rating it drives through; <see cref="NavAccess.AllScenery"/> passes all.</summary>
        public readonly byte CrushLimit;

        public readonly float Radius;

        public MovementClass(NavLayer layer, byte crushLimit, float radius)
        {
            Layer = layer;
            CrushLimit = crushLimit;
            Radius = radius;
        }

        /// <summary>
        /// The <see cref="NavField"/> key it reads: the same layer and crush limit at radius 0, as the radius only moves
        /// the open threshold.
        /// </summary>
        public MovementClass FieldKey => new(Layer, CrushLimit, 0f);

        /// <summary>Whether a cell of this surface and scenery rating is a wall to the class.</summary>
        public bool Blocks(NavSurface surface, byte scenery)
        {
            if ((surface & NavLayers.Surfaces(Layer)) == 0)
            {
                return true;
            }

            return CrushLimit != NavAccess.AllScenery && scenery > CrushLimit;
        }

        public bool Equals(MovementClass other) =>
            Layer == other.Layer && CrushLimit == other.CrushLimit && Radius.Equals(other.Radius);

        public override bool Equals(object obj) => obj is MovementClass other && Equals(other);

        public override int GetHashCode() => (int)math.hash(new float3((float)Layer, CrushLimit, Radius));
    }
}
