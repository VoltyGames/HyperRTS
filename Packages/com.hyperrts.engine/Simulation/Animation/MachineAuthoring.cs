using HyperRTS.Core;
using HyperRTS.Simulation.GameEntities;
using HyperRTS.Simulation.Common;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Simulation.Animation
{
    /// <summary>The root of an animated machine (pumpjack, wind turbine, launcher outriggers); its children carry Machine Parts.</summary>
    [AddComponentMenu(HyperRTSMenu.Animation + "Machine")]
    [Icon(HyperRTSIcons.Animation)]
    [HelpURL(HyperRTSDocs.Animation)]
    [DisallowMultipleComponent]
    public class MachineAuthoring : AuthoringBehaviour
    {
        [Tooltip("Run only while a player owns it (a captured derrick pumps, a neutral one stands still).")]
        public bool runsWhenOwned = true;

        [Tooltip("Always, while its building is complete and powered, while it is producing, while it is firing, or as its unit deploys.")]
        public MachineRun runs;

        [Tooltip("Seconds to ease from a stop to full speed, and back.")]
        [Min(0.1f)]
        public float rampSeconds = 2f;

        public class Baker : Baker<MachineAuthoring>
        {
            public override void Bake(MachineAuthoring authoring)
            {
                var writer = new BakerWriter(this, GetEntity(TransformUsageFlags.Dynamic));
                var owner = GetComponentInParent<GameEntityAuthoring>();
                writer.Add(new MachineClock
                {
                    OwnedOnly = authoring.runsWhenOwned,
                    Run = authoring.runs,
                    Owner = owner == null ? Entity.Null : GetEntity(owner, TransformUsageFlags.Dynamic),
                    RampSeconds = math.max(authoring.rampSeconds, 0.1f),
                });
            }
        }
    }
}
