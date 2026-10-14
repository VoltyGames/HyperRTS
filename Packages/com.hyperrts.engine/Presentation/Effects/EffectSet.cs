using HyperRTS.Simulation.Effects;

namespace HyperRTS.Presentation.Effects
{
    /// <summary>One entity type's effects: a rig per slot, and what its shots throw up where they land.</summary>
    public sealed class EffectSet
    {
        private readonly EntityEffect[] _effects;

        public EffectSet(EntityEffect[] effects, SurfaceEffects impacts)
        {
            _effects = effects;
            Impacts = impacts;
        }

        /// <summary>Null when the type's shots throw nothing up.</summary>
        public SurfaceEffects Impacts { get; }

        /// <summary>The slot's rig entry; false when the type has none.</summary>
        public bool TryGet(EffectSlot slot, out EntityEffect effect)
        {
            foreach (var entry in _effects)
            {
                if (entry.Slot == slot)
                {
                    effect = entry;
                    return true;
                }
            }

            effect = default;
            return false;
        }
    }
}
