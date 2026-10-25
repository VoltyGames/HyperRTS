using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>
    /// Limited rounds for the weapon: each shot uses one and an empty weapon holds fire (an aircraft with a pad flies
    /// home). Rounds come back while docked, anywhere one at a time, or all at once after the magazine runs empty.
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.Combat + "Ammo")]
    [Icon(HyperRTSIcons.Combat)]
    [HelpURL(HyperRTSDocs.Combat)]
    [DisallowMultipleComponent]
    [RequiresAuthoring(typeof(WeaponAuthoring), "a Weapon")]
    public class AmmoAuthoring : AuthoringBehaviour
    {
        [Tooltip("Rounds when full.")]
        [Min(1)]
        public int rounds = 4;

        [Tooltip("Seconds to reload one round, or the whole magazine with Magazine.")]
        [Min(0f)]
        public float reloadTime = 1f;

        [Tooltip("Docked reloads by the round only at an airfield, Round reloads by the round anywhere, Magazine reloads every round at once once empty.")]
        public AmmoRefill refill;

        public class Baker : Baker<AmmoAuthoring>
        {
            public override void Bake(AmmoAuthoring authoring)
            {
                var writer = new BakerWriter(this, GetEntity(TransformUsageFlags.Dynamic));
                WeaponSetup.AddAmmo(ref writer, authoring.rounds, authoring.reloadTime, authoring.refill);
            }
        }
    }
}
