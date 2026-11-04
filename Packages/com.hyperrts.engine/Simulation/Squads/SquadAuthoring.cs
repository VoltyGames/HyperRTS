using System;
using System.Collections.Generic;
using HyperRTS.Core;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.GameEntities;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Simulation.Squads
{
    /// <summary>
    /// Makes a unit a squad of soldiers who each carry one weapon and take their own hits, while the squad stays one
    /// entity for orders, movement and capture. The unit's max health is shared evenly among the members.
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.Units + "Squad")]
    [Icon(HyperRTSIcons.Units)]
    [HelpURL(HyperRTSDocs.Combat)]
    [DisallowMultipleComponent]
    [RequiresAuthoring(typeof(UnitAuthoring), "a Unit")]
    [RequiresAuthoring(typeof(WeaponAuthoring), "a Weapon")]
    public class SquadAuthoring : AuthoringBehaviour
    {
        [Tooltip("The soldiers, in figure order. Each fires one shot per cycle with its weapon.")]
        public List<Member> members = new();

        /// <summary>One soldier of the squad.</summary>
        [Serializable]
        public class Member
        {
            [Tooltip("0 is the unit's primary weapon, 1 and up its extra weapons in order.")]
            [Min(0)]
            public int weapon;
        }

        /// <summary>Adds the members with <paramref name="maxHealth"/> shared evenly, standing in a ring, each carrying the weapon at its index.</summary>
        public static void Add(EntityWriter writer, float maxHealth, IReadOnlyList<byte> weapons)
        {
            var each = maxHealth / weapons.Count;
            writer.Add(default(Suppression));
            writer.Add(Pace.Normal);
            writer.Add(new Reinforcing());
            writer.SetEnabled<Reinforcing>(false);
            writer.Add(new SquadWork());
            writer.SetEnabled<SquadWork>(false);
            var buffer = writer.AddBuffer<SquadMember>();
            for (var i = 0; i < weapons.Count; i++)
            {
                var slot = SquadMember.SpreadSlot(i, weapons.Count);
                buffer.Add(new SquadMember
                {
                    Health = each, MaxHealth = each, Offset = slot, Slot = slot, Weapon = weapons[i],
                });
            }
        }

        public class Baker : Baker<SquadAuthoring>
        {
            public override void Bake(SquadAuthoring authoring)
            {
                var unit = GetComponent<UnitAuthoring>();
                var weapons = new List<byte>();
                foreach (var member in authoring.members)
                {
                    weapons.Add((byte)math.clamp(member.weapon, 0, byte.MaxValue));
                }

                Add(this.Writer(), unit.maxHealth, weapons);
            }
        }
    }
}
