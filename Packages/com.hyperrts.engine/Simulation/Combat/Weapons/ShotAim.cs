using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Combat.Weapons
{
    /// <summary>Where a weapon aims this frame: an entity and the point it stands at, or a ground point alone.</summary>
    public struct ShotAim
    {
        public Entity Target;
        public float3 Point;

        /// <summary>False when the weapon has nothing to aim at.</summary>
        public bool Exists;

        public static ShotAim At(float3 point) => new() { Point = point, Exists = true };

        /// <summary>
        /// The target entity; else <paramref name="ordered"/> when it is set; else the point the running salvo last
        /// aimed at, which a weapon that launches rounds at ground finishes even without a target.
        /// </summary>
        public static ShotAim Choose(Entity target, in ComponentLookup<LocalTransform> transforms, in Weapon weapon,
            bool hasOrdered, float3 ordered)
        {
            if (transforms.TryGetComponent(target, out var place))
            {
                return new ShotAim { Target = target, Point = place.Position, Exists = true };
            }

            if (hasOrdered && weapon.FiresAtGround)
            {
                return At(ordered);
            }

            return weapon.SalvoShot > 0 && weapon.FiresAtGround ? At(weapon.SalvoAim) : default;
        }
    }
}
