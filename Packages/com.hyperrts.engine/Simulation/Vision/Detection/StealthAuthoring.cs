using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.GameEntities;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Vision.Detection
{
    /// <summary>Hides this unit or building from hostile teams unless one of their detectors covers it.</summary>
    [AddComponentMenu(HyperRTSMenu.Vision + "Stealth")]
    [Icon(HyperRTSIcons.Vision)]
    [HelpURL(HyperRTSDocs.Vision)]
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

        [Tooltip("Hidden only in woods, tall crops and ruins, where enemy ground units close by and detectors still see it.")]
        public bool inConcealment;

        public class Baker : Baker<StealthAuthoring>
        {
            public override void Bake(StealthAuthoring authoring)
            {
                var writer = this.Writer();
                StealthSetup.AddStealth(writer, new Stealth
                {
                    RevealDuration = authoring.revealAfterFiring,
                    OnlyWhenStill = authoring.onlyWhenStill,
                    InConcealment = authoring.inConcealment,
                }, authoring.startEnabled);
            }
        }
    }
}
