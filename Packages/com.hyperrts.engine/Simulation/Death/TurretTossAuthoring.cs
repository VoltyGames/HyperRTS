using HyperRTS.Simulation.Common.Authoring;
using System;
using System.Collections.Generic;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.GameEntities;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Death
{
    /// <summary>
    /// A vehicle that throws its turret when it dies to a catastrophic hit (a magazine going up). Its wreck is then
    /// the hull without a turret, and the turret lands nearby as remains that block their cell.
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.Entity + "Destruction/Turret Toss")]
    [Icon(HyperRTSIcons.TurretTossAuthoring)]
    [HelpURL(HyperRTSDocs.Combat)]
    [DisallowMultipleComponent]
    [RequiresAuthoring(typeof(GameEntityAuthoring), "a unit")]
    public class TurretTossAuthoring : AuthoringBehaviour
    {
        [Tooltip("Death spawn used instead of the usual one after a catastrophic hit: the wreck without its turret. A ghost.")]
        [GhostPrefab("Hull")]
        public GameObject hull;

        [Tooltip("Remains of the thrown turret. A ghost whose blocker holds its cell.")]
        [GhostPrefab("Turret")]
        public GameObject turret;

        [Tooltip("A lethal hit of any of these damage types is catastrophic.")]
        public List<DamageType> damageTypes = new();

        [Tooltip("A lethal hit that overshoots the unit's health by this share of its maximum is catastrophic, whatever its type.")]
        [Min(0.01f)]
        public float overkill = 1f;

        [Tooltip("Metres the turret lands from the hull: nearest and farthest.")]
        public Vector2 distance = new(4f, 9f);

        [Tooltip("Degrees across the way the hit pushed that the turret may land in.")]
        [Range(1f, 360f)]
        public float arc = 120f;

        [Tooltip("Metres the turret rises above its throw line at the top of its flight.")]
        [Min(0.1f)]
        public float height = 6f;

        [Tooltip("Seconds the turret takes to land.")]
        [Min(0.1f)]
        public float duration = 1.4f;

        public static void Add(EntityWriter writer, string owner, in TurretToss toss,
            IReadOnlyList<DamageType> types)
        {
            if (toss.MaxDistance < toss.MinDistance || toss.MinDistance < 0f)
            {
                throw new ArgumentException($"Turret Toss on '{owner}': distance must run from near to far, above 0.");
            }

            if (toss.Overkill <= 0f || toss.Duration <= 0f)
            {
                throw new ArgumentException($"Turret Toss on '{owner}': overkill and duration must be above 0.");
            }

            writer.Add(toss);
            writer.Add<LethalHit>();
            var buffer = writer.AddBuffer<TossType>();
            foreach (var type in types)
            {
                buffer.Add(new TossType { DamageType = type });
            }
        }

        public class Baker : Baker<TurretTossAuthoring>
        {
            public override void Bake(TurretTossAuthoring authoring)
            {
                if (authoring.hull == null || authoring.turret == null)
                {
                    throw new InvalidOperationException($"Turret Toss on '{authoring.name}' needs a hull and a turret.");
                }

                foreach (var type in authoring.damageTypes)
                {
                    DependsOn(type);
                }

                var writer = this.Writer();
                TurretTossAuthoring.Add(writer, authoring.name, new TurretToss
                {
                    Hull = GetEntity(authoring.hull, TransformUsageFlags.Dynamic),
                    Turret = GetEntity(authoring.turret, TransformUsageFlags.Dynamic),
                    Overkill = authoring.overkill,
                    MinDistance = authoring.distance.x,
                    MaxDistance = authoring.distance.y,
                    Arc = Mathf.Deg2Rad * authoring.arc,
                    Height = authoring.height,
                    Duration = authoring.duration,
                }, authoring.damageTypes);
            }
        }
    }
}
