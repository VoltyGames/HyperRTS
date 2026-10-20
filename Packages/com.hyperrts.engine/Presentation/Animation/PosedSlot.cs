using System;
using UnityEngine;

namespace HyperRTS.Presentation.Animation
{
    /// <summary>A <see cref="PartSlot"/> that follows one weapon's <c>WeaponPose</c>.</summary>
    [Serializable]
    public abstract class PosedSlot : PartSlot
    {
        [Tooltip("Weapon pose it follows: 0 is the primary weapon, 1 and up the extra weapons in order.")]
        [Min(0)]
        public int pose;

        public override string Problem(MotionRigAuthoring rig)
        {
            var problem = base.Problem(rig);
            if (problem != null || pose < rig.WeaponCount)
            {
                return problem;
            }

            return $"Part '{part.name}' follows weapon pose {pose}, but the unit has {rig.WeaponCount} weapons.";
        }
    }
}
