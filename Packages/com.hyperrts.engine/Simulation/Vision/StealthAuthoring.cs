using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.GameEntities;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Vision
{
    /// <summary>Hides this unit or building from hostile teams unless one of their detectors covers it.</summary>
    [AddComponentMenu(HyperRTSMenu.Vision + "Stealth")]
    [Icon(HyperRTSIcons.Vision)]
    [HelpURL(HyperRTSDocs.Modules)]
    [DisallowMultipleComponent]
    [RequiresAuthoring(typeof(GameEntityAuthoring), "a Unit or Building")]
    public class StealthAuthoring : AuthoringBehaviour
    {
        [Tooltip("Stealthed from the start. Off for stealth a game grants later (upgrade, ability, field).")]
        public bool startEnabled = true;

        [Tooltip("Seconds the entity stays visible after firing. 0 = firing never reveals it.")]
        [Min(0f)]
        public float revealAfterFiring = 3f;

        [Tooltip("Visible while moving; stealthed only when standing still.")]
        public bool onlyWhenStill;

        public class Baker : Baker<StealthAuthoring>
        {
            public override void Bake(StealthAuthoring authoring)
            {
                var writer = new BakerWriter(this, GetEntity(TransformUsageFlags.Dynamic));
                StealthSetup.AddStealth(ref writer, new Stealth
                {
                    RevealDuration = authoring.revealAfterFiring,
                    OnlyWhenStill = authoring.onlyWhenStill,
                }, authoring.startEnabled);
            }
        }
    }
}
