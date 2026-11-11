using HyperRTS.Simulation.Common.Infantry;
using System;
using System.Collections.Generic;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Common.Combat;
using HyperRTS.Simulation.GameEntities;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Simulation.Squads
{
    /// <summary>
    /// Makes a unit a squad of soldiers who each carry one weapon and take their own hits, while the squad stays one
    /// entity for orders, movement and capture. The unit's max health is shared evenly among the members.
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.Entity + "Units/Squad")]
    [Icon(HyperRTSIcons.SquadAuthoring)]
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

            [Tooltip("The extra weapon it fires instead while that weapon is enabled, such as by research. Off keeps its own weapon.")]
            public bool hasUpgradedWeapon;

            [Tooltip("The upgraded weapon's mount index, as for Weapon.")]
            [Min(0)]
            public int upgradedWeapon;

            [Tooltip("The designation of the weapon it carries, shown on its card (M4A1, PKP). Empty for none.")]
            public string designation;

            [Header("Deployed")]
            [Tooltip("Stands at the crew place below while the unit's weapon is set up, instead of its slot in the squad.")]
            public bool takesCrewPlace;

            [Tooltip("Metres in the unit's frame, x to the right and y ahead, such as 0.6 behind the gun.")]
            public Vector2 crewOffset;

            [Tooltip("How it holds itself at the crew place. Fire and idle use the posture's clips.")]
            public Posture crewPosture = Posture.Prone;
        }

        /// <summary>
        /// Adds the members with <paramref name="maxHealth"/> shared evenly, in a ring. Null <paramref
        /// name="upgrades"/>, <paramref name="crews"/> and <paramref name="designations"/> mean none.
        /// </summary>
        public static void Add(EntityWriter writer, float maxHealth, IReadOnlyList<byte> weapons,
            IReadOnlyList<byte?> upgrades = null, IReadOnlyList<CrewPlace> crews = null,
            IReadOnlyList<FixedString32Bytes> designations = null)
        {
            var each = maxHealth / weapons.Count;
            writer.Add(default(Suppression));
            writer.Add(Pace.Normal);
            writer.Add(new Reinforcing());
            writer.SetEnabled<Reinforcing>(false);
            writer.Add(new Retreating());
            writer.SetEnabled<Retreating>(false);
            writer.Add(new SquadWork());
            writer.SetEnabled<SquadWork>(false);
            var buffer = writer.AddBuffer<SquadMember>();
            for (var i = 0; i < weapons.Count; i++)
            {
                buffer.Add(new SquadMember
                {
                    Health = each, MaxHealth = each, Weapon = weapons[i],
                    BaseWeapon = weapons[i],
                    UpgradedWeapon = upgrades?[i] ?? weapons[i],
                    Designation = designations != null ? designations[i] : default,
                    TakesCrewPlace = crews != null && crews[i].Takes,
                    CrewOffset = crews != null ? crews[i].Offset : default,
                    CrewPosture = crews != null ? crews[i].Posture : default,
                });
            }
        }

        public class Baker : Baker<SquadAuthoring>
        {
            public override void Bake(SquadAuthoring authoring)
            {
                var unit = GetComponent<UnitAuthoring>();
                var weapons = new List<byte>();
                var upgrades = new List<byte?>();
                var crews = new List<CrewPlace>();
                var designations = new List<FixedString32Bytes>();
                foreach (var member in authoring.members)
                {
                    weapons.Add((byte)member.weapon);
                    upgrades.Add(member.hasUpgradedWeapon ? (byte)member.upgradedWeapon : null);
                    designations.Add(member.designation);
                    crews.Add(new CrewPlace { Takes = member.takesCrewPlace, Offset = member.crewOffset, Posture = member.crewPosture });
                }

                Add(this.Writer(), unit.maxHealth, weapons, upgrades, crews, designations);
            }
        }
    }
}
