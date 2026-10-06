using HyperRTS.Core;
using HyperRTS.Simulation.Air;
using HyperRTS.Simulation.Common;
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

        [Tooltip("Surface the unit moves on: Ground (land and decks), Naval (water), Amphibious (both) or Air (flies over everything).")]
        public NavLayer navLayer;

        [Tooltip("Population this unit uses while alive.")]
        [Min(0)]
        public int population = 1;

        [Header("Flight (Air layer only)")]
        [Tooltip("Cruise height above the ground or water.")]
        [Min(0f)]
        public float flightAltitude = 12f;

        [Tooltip("Vertical speed for take-off, landing and following the terrain.")]
        [Min(0.1f)]
        public float climbSpeed = 6f;

        [Tooltip("0 hovers when idle (helicopters); otherwise keeps circling at this radius (jets).")]
        [Min(0f)]
        public float loiterRadius;

        [Tooltip("Takes a pad at the Airfield that builds it, flies back to it to rearm (Return order or out of ammo) and docks there.")]
        public bool usesLandingPads;

        public override float Radius => radius;

        protected override int Population => population;

        public class Baker : Baker<UnitAuthoring>
        {
            public override void Bake(UnitAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                authoring.BakeGameEntity(this, entity);

                var sink = new BakerSink(this, entity);
                UnitSetup.Add(ref sink, authoring.moveSpeed, authoring.radius, authoring.navLayer);
                if (authoring.navLayer == NavLayer.Air)
                {
                    sink.Add(new Flight
                    {
                        Altitude = authoring.flightAltitude,
                        ClimbSpeed = authoring.climbSpeed,
                        LoiterRadius = authoring.loiterRadius,
                    });

                    if (authoring.usesLandingPads)
                    {
                        AirSetup.AddPadUser(ref sink);
                    }
                }
            }
        }
    }

    /// <summary>Marks a movable, orderable unit.</summary>
    public struct UnitTag : IComponentData { }
}
