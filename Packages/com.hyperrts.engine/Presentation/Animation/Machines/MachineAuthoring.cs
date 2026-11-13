using System;
using HyperRTS.Simulation.Air;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.GameEntities;
using HyperRTS.Simulation.Production;
using HyperRTS.Simulation.Resources.Gathering;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Presentation.Animation.Machines
{
    /// <summary>The root of an animated machine (pumpjack, wind turbine, launcher outriggers); its children carry Machine Parts.</summary>
    [AddComponentMenu(HyperRTSMenu.Look + "Animation/Machine")]
    [Icon(HyperRTSIcons.MachineAuthoring)]
    [HelpURL(HyperRTSDocs.Animation)]
    [DisallowMultipleComponent]
    public class MachineAuthoring : AuthoringBehaviour
    {
        [Tooltip("Run only while a player owns it (a captured derrick pumps, a neutral one stands still).")]
        public bool runsWhenOwned = true;

        [Tooltip("Always, while its building is complete and powered, while it is producing, while it is firing, as its unit deploys, after its producer releases a unit, while its aircraft drops, while its builder builds, while its harvester gathers or unloads, or as its harvester's cargo fills and drains.")]
        public MachineRun runs;

        [Tooltip("Seconds to ease from a stop to full speed, and back.")]
        [Min(0.1f)]
        public float rampSeconds = 2f;

        [Tooltip("Firing, Releasing and Building: how long it keeps running after the last shot, release or work (a door counts its opening time).")]
        [Unit("s")]
        [Min(0f)]
        public float keepRunningSeconds = 1f;

        public class Baker : Baker<MachineAuthoring>
        {
            public override void Bake(MachineAuthoring authoring)
            {
                if (authoring.runs == MachineRun.Releasing && GetComponentInParent<ProducerAuthoring>() == null)
                {
                    throw new InvalidOperationException($"Machine on '{authoring.name}' runs on releases but has no Producer above it.");
                }

                if (authoring.runs == MachineRun.Dropping && GetComponentInParent<FlightAuthoring>() == null)
                {
                    throw new InvalidOperationException($"Machine on '{authoring.name}' runs on drops but has no Flight above it.");
                }

                if (authoring.runs == MachineRun.Building && GetComponentInParent<BuilderAuthoring>() == null)
                {
                    throw new InvalidOperationException($"Machine on '{authoring.name}' runs while building but has no Builder above it.");
                }

                var needsHarvester = authoring.runs is MachineRun.Gathering or MachineRun.Unloading or MachineRun.Loaded;
                if (needsHarvester && GetComponentInParent<HarvesterAuthoring>() == null)
                {
                    throw new InvalidOperationException($"Machine on '{authoring.name}' runs on {authoring.runs} but has no Harvester above it.");
                }

                var writer = this.Writer();
                var owner = GetComponentInParent<GameEntityAuthoring>();
                writer.Add(new MachineClock
                {
                    OwnedOnly = authoring.runsWhenOwned,
                    Run = authoring.runs,
                    Owner = owner == null ? Entity.Null : GetEntity(owner, TransformUsageFlags.Dynamic),
                    RampSeconds = authoring.rampSeconds,
                    KeepRunningSeconds = authoring.keepRunningSeconds,
                });
            }
        }
    }
}
