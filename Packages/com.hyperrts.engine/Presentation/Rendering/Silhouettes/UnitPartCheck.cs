using HyperRTS.Simulation.Common;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Presentation.Rendering.Silhouettes
{
    /// <summary>Finds whether a rendered entity is part of a unit, by walking up its parents to a unit.</summary>
    public struct UnitPartCheck
    {
        public ComponentLookup<Parent> Parents;
        public ComponentLookup<UnitTag> Units;

        /// <summary>Whether a unit sits at or above the entity.</summary>
        public bool IsUnitPart(Entity entity)
        {
            while (true)
            {
                if (Units.HasComponent(entity))
                {
                    return true;
                }

                if (!Parents.TryGetComponent(entity, out var parent))
                {
                    return false;
                }

                entity = parent.Value;
            }
        }
    }
}
