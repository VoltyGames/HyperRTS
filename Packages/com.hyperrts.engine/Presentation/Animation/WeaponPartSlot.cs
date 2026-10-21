using System;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Serialization;

namespace HyperRTS.Presentation.Animation
{
    /// <summary>A <see cref="PartSlot"/> that follows one weapon's <c>WeaponPose</c>.</summary>
    [Serializable]
    public abstract class WeaponPartSlot : PartSlot
    {
        [Tooltip("Weapon it follows: 0 is the primary weapon, 1 and up the extra weapons in order.")]
        [FormerlySerializedAs("pose")]
        [Min(0)]
        public int weapon;

        public override string Problem(MotionRigAuthoring rig)
        {
            var problem = base.Problem(rig);
            if (problem != null || weapon < rig.WeaponCount)
            {
                return problem;
            }

            return $"Part '{part.name}' follows weapon {weapon}, but the unit has {rig.WeaponCount} weapons.";
        }

        protected override MotionRigPart AtRest(Entity entity, MotionPartKind kind)
        {
            var baked = base.AtRest(entity, kind);
            baked.Weapon = (byte)weapon;
            return baked;
        }
    }
}
