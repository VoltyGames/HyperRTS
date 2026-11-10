using HyperRTS.Simulation.Air;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Movement;
using HyperRTS.Simulation.Movement.Crushing;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Drive;
using HyperRTS.Simulation.Navigation.Grid;
using HyperRTS.Simulation.Navigation.Paths;
using HyperRTS.Simulation.Orders;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.GameEntities
{
    /// <summary>
    /// A movable, orderable unit. Add a collider so it can be clicked, plus optional modules
    /// (Weapon, Harvester, Builder) for what it can do.
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.Entity + "Units/Unit")]
    [Icon(HyperRTSIcons.UnitAuthoring)]
    [HelpURL(HyperRTSDocs.Movement)]
    [DisallowMultipleComponent]
    public class UnitAuthoring : GameEntityAuthoring
    {
        // No header: it continues the base class's Production group.
        [Tooltip("Population this unit uses while alive.")]
        [Min(0)]
        public int population = 1;

        [Header("Movement")]
        [Tooltip("Top speed.")]
        [Unit("m/s")]
        [Min(0f)]
        public float moveSpeed = 5f;

        [Tooltip("Radius used for avoidance, formations and spatial queries. For a long hull, half its width.")]
        [Min(0.05f)]
        public float radius = 0.5f;

        [Tooltip("Full hull length along the unit's forward: avoidance treats it as a capsule this long and two radii wide. 0 (or up to the diameter) keeps a circle.")]
        [Min(0f)]
        public float hullLength;

        [Tooltip("Gap kept clear between this unit's footprint and its neighbours'. Vehicles stop short of touching; infantry can stand closer.")]
        [Min(0f)]
        public float spacing = DefaultSpacing;

        [Tooltip("Surface the unit moves on: Ground (land and decks), Naval (water) or Amphibious (both). A Flight component makes it Air.")]
        public NavLayer navLayer;

        [Tooltip("Mass in tonnes, from the real vehicle; a squad is 1. The heavier of two units by half again has the right of way.")]
        [Unit("t")]
        [Min(0f)]
        public float mass;

        [Tooltip("Walks between destructible scenery (trees, fences) as infantry do. Otherwise scenery blocks it unless a Crusher flattens it.")]
        public bool walksThroughScenery;

        /// <summary>Vehicles' gap: they stop short of touching, as crews keep their distance.</summary>
        public const float DefaultSpacing = 0.6f;

        public override float Radius => radius;

        protected override int Population => population;

        public static void Add(EntityWriter writer, float moveSpeed, NavAgent agent)
        {
            writer.Add<UnitTag>();
            writer.Add(new MovementSpeed { Value = moveSpeed });
            writer.Add<MoveDestination>();
            writer.SetEnabled<MoveDestination>(false);
            writer.Add(agent);
            writer.AddBuffer<PathWaypoint>();
            writer.Add<PathState>();
            writer.Add<ActiveOrder>();
            writer.SetEnabled<ActiveOrder>(false);
            writer.AddBuffer<QueuedOrder>();
            writer.Add<MoveOrderState>();
            writer.SetEnabled<MoveOrderState>(false);
            writer.Add<Arrival>();
            writer.SetEnabled<Arrival>(false);
            writer.Add<MoveProgress>();
            writer.Add<Velocity>();
            writer.Add<FacingGoal>();
            writer.SetEnabled<FacingGoal>(false);
        }

        public class Baker : Baker<UnitAuthoring>
        {
            public override void Bake(UnitAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                authoring.BakeGameEntity(this, entity);

                var writer = this.Writer(entity);
                var agent = new NavAgent
                {
                    Radius = authoring.radius,
                    HullLength = authoring.hullLength,
                    Spacing = authoring.spacing,
                    Layer = Layer(authoring),
                    SceneryCrushLimit = SceneryCrushLimit(authoring),
                    Mass = authoring.mass,
                };
                var locomotion = GetComponent<LocomotionAuthoring>();
                if (locomotion != null)
                {
                    agent.Chassis = locomotion.Chassis;
                }

                agent.WallMargin = agent.HullWallMargin;
                UnitAuthoring.Add(writer, authoring.moveSpeed, agent);
            }

            private byte SceneryCrushLimit(UnitAuthoring authoring)
            {
                if (authoring.walksThroughScenery)
                {
                    return NavAccess.AllScenery;
                }

                var crusher = GetComponent<CrusherAuthoring>();
                return crusher != null ? (byte)Mathf.Clamp(crusher.size, 0, NavAccess.AllScenery - 1) : (byte)0;
            }

            // Flight bakes the Flight component, so its presence alone makes the unit an aircraft.
            private NavLayer Layer(UnitAuthoring authoring) =>
                GetComponent<FlightAuthoring>() != null ? NavLayer.Air : authoring.navLayer;
        }
    }
}
