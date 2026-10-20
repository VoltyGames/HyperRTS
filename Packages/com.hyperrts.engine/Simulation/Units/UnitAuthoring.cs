using HyperRTS.Core;
using HyperRTS.Simulation.Air;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.GameEntities;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Orders;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Units
{
    /// <summary>
    /// A movable, orderable unit. Add a collider so it can be clicked, plus optional modules
    /// (Weapon, Harvester, Builder) for what it can do.
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.Units + "Unit")]
    [Icon(HyperRTSIcons.Units)]
    [HelpURL(HyperRTSDocs.Units)]
    [DisallowMultipleComponent]
    public class UnitAuthoring : GameEntityAuthoring
    {
        // No header: it continues the base class's Production group.
        [Tooltip("Population this unit uses while alive.")]
        [Min(0)]
        public int population = 1;

        [Header("Movement")]
        [Tooltip("Top speed in world units per second.")]
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

        /// <summary>Vehicles' gap: they stop short of touching, as crews keep their distance.</summary>
        public const float DefaultSpacing = 0.6f;

        public override float Radius => radius;

        protected override int Population => population;

        public static void Add<TWriter>(ref TWriter writer, float moveSpeed, NavAgent agent)
            where TWriter : struct, IEntityWriter
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
        }

        public class Baker : Baker<UnitAuthoring>
        {
            public override void Bake(UnitAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                authoring.BakeGameEntity(this, entity);

                var writer = new BakerWriter(this, entity);
                UnitAuthoring.Add(ref writer, authoring.moveSpeed, new NavAgent
                {
                    Radius = authoring.radius,
                    HullLength = authoring.hullLength,
                    Spacing = authoring.spacing,
                    Layer = Layer(authoring),
                });
            }

            // Flight bakes the Flight component, so its presence alone makes the unit an aircraft.
            private NavLayer Layer(UnitAuthoring authoring) =>
                GetComponent<FlightAuthoring>() != null ? NavLayer.Air : authoring.navLayer;
        }
    }
}
