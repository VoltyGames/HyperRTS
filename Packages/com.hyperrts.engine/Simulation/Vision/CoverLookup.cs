using HyperRTS.Simulation.Common;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Vision
{
    /// <summary>Reads each entity's <see cref="Cover"/>: stealth first, then concealment.</summary>
    public struct CoverLookup
    {
        [ReadOnly] private ComponentLookup<Stealthed> _stealthed;
        [ReadOnly] private ComponentLookup<Concealed> _concealed;

        public CoverLookup(ref SystemState state)
        {
            _stealthed = state.GetComponentLookup<Stealthed>(true);
            _concealed = state.GetComponentLookup<Concealed>(true);
        }

        public void Update(ref SystemState state)
        {
            _stealthed.Update(ref state);
            _concealed.Update(ref state);
        }

        public readonly Cover Find(Entity entity)
        {
            if (_stealthed.HasEnabled(entity))
            {
                return Cover.Stealthed;
            }

            return _concealed.HasEnabled(entity) ? Cover.Concealed : Cover.None;
        }
    }
}
