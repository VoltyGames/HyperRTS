using HyperRTS.Core;
using HyperRTS.Simulation.Air;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.GameEntities;
using HyperRTS.Simulation.Navigation;
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
    [HelpURL(HyperRTSDocs.GettingStarted)]
    [DisallowMultipleComponent]
    public class UnitAuthoring : GameEntityAuthoring
    {
        [Header("Movement")]
        [Tooltip("Top speed in world units per second.")]
        [Min(0f)]
        public float moveSpeed = 5f;

        [Tooltip("Radius used for avoidance, formations and spatial queries.")]
        [Min(0.05f)]
        public float radius = 0.5f;

        [Tooltip("Surface the unit moves on: Ground (land and decks), Naval (water) or Amphibious (both). A Flight component makes it Air.")]
        public NavLayer navLayer;

        [Tooltip("Population this unit uses while alive.")]
        [Min(0)]
        public int population = 1;

        public override float Radius => radius;

        protected override int Population => population;

        public class Baker : Baker<UnitAuthoring>
        {
            public override void Bake(UnitAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                authoring.BakeGameEntity(this, entity);

                var sink = new BakerSink(this, entity);
                UnitSetup.Add(ref sink, authoring.moveSpeed, authoring.radius, Layer(authoring));
            }

            // Flight bakes the Flight component, so its presence alone makes the unit an aircraft.
            private NavLayer Layer(UnitAuthoring authoring) =>
                GetComponent<FlightAuthoring>() != null ? NavLayer.Air : authoring.navLayer;
        }
    }
}
