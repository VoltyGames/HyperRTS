using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>
    /// Limited rounds for the weapon: each shot uses one, an empty weapon holds fire (an aircraft with a pad flies
    /// home), and rounds reload one at a time while the aircraft is docked.
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.Combat + "Ammo")]
    [Icon(HyperRTSIcons.Combat)]
    [HelpURL(HyperRTSDocs.Modules)]
    [DisallowMultipleComponent]
    [RequiresAuthoring(typeof(WeaponAuthoring), "a Weapon")]
    public class AmmoAuthoring : AuthoringBehaviour
    {
        [Tooltip("Rounds when full.")]
        [Min(1)]
        public int rounds = 4;

        [Tooltip("Seconds to reload one round while docked.")]
        [Min(0f)]
        public float reloadTime = 1f;

        public class Baker : Baker<AmmoAuthoring>
        {
            public override void Bake(AmmoAuthoring authoring)
            {
                var sink = new BakerSink(this, GetEntity(TransformUsageFlags.Dynamic));
                WeaponSetup.AddAmmo(ref sink, authoring.rounds, authoring.reloadTime);
            }
        }
    }

    /// <summary>Rounds left in the weapon; refilled while docked.</summary>
    public struct Ammo : IComponentData
    {
        [GhostField] public int Max;
        [GhostField] public int Current;
        public float ReloadTime;
        public float ReloadElapsed;

        /// <summary>Weapons without ammo never run dry.</summary>
        public static bool IsEmpty(in ComponentLookup<Ammo> lookup, Entity entity) =>
            lookup.TryGetComponent(entity, out var ammo) && ammo.Current <= 0;
    }
}
