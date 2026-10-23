using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Cues;
using HyperRTS.Simulation.Units;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Air
{
    /// <summary>
    /// Makes the unit an aircraft: it moves on the Air layer, flying straight over everything at its cruise height,
    /// and can take a pad at the Airfield that builds it.
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.Units + "Flight")]
    [Icon(HyperRTSIcons.Units)]
    [HelpURL(HyperRTSDocs.Air)]
    [DisallowMultipleComponent]
    [RequiresAuthoring(typeof(UnitAuthoring), "a Unit")]
    public class FlightAuthoring : AuthoringBehaviour
    {
        [Tooltip("Cruise height above the ground or water.")]
        [Min(0f)]
        public float altitude = 12f;

        [Tooltip("Vertical speed for take-off, landing and following the terrain.")]
        [Min(0.1f)]
        public float climbSpeed = 6f;

        [Tooltip("0 hovers when idle (helicopters); otherwise keeps circling at this radius (jets).")]
        [Min(0f)]
        public float loiterRadius;

        [Tooltip("Takes a pad at the Airfield that builds it, flies back to it to reload (Return order or out of ammo) and docks there.")]
        public bool usesLandingPads;

        [Tooltip("Optional child that airdrop crates and strike bombs leave from, such as a ramp or bomb bay; the origin "
            + "without one.")]
        public Transform releasePoint;

        public class Baker : Baker<FlightAuthoring>
        {
            public override void Bake(FlightAuthoring authoring)
            {
                var writer = new BakerWriter(this, GetEntity(TransformUsageFlags.Dynamic));
                AirSetup.AddFlight(ref writer, authoring.altitude, authoring.climbSpeed, authoring.loiterRadius,
                    EffectSetup.Socket(this, authoring.transform, authoring.releasePoint));
                if (authoring.usesLandingPads)
                {
                    AirSetup.AddPadUser(ref writer);
                }
            }
        }
    }
}
