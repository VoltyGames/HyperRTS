using HyperRTS.Core;
using HyperRTS.Simulation.Common;
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
    public class StealthAuthoring : MonoBehaviour
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
                var sink = new BakerSink(this, GetEntity(TransformUsageFlags.Dynamic));
                StealthSetup.AddStealth(ref sink, new Stealth
                {
                    RevealDuration = authoring.revealAfterFiring,
                    OnlyWhenStill = authoring.onlyWhenStill,
                }, authoring.startEnabled);
            }
        }
    }

    /// <summary>
    /// Stealth rules of an entity. Enabled while it has stealth at all, which games toggle at runtime;
    /// <see cref="StealthSystem"/> turns that into <see cref="Stealthed"/>.
    /// </summary>
    public struct Stealth : IComponentData, IEnableableComponent
    {
        public float RevealDuration;
        public bool OnlyWhenStill;

        /// <summary>Seconds left visible after firing.</summary>
        public float RevealTimer;
    }
}
