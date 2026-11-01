using HyperRTS.Simulation.Common;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Vision
{
    /// <summary>Reads what decides whether a team sees an entity: its <see cref="Cover"/> and whether it is a <see cref="TallTarget"/>.</summary>
    public struct CoverLookup
    {
        [ReadOnly] private ComponentLookup<Stealthed> _stealthed;
        [ReadOnly] private ComponentLookup<Concealed> _concealed;
        [ReadOnly] private ComponentLookup<TallTarget> _tall;

        public CoverLookup(ref SystemState state)
        {
            _stealthed = state.GetComponentLookup<Stealthed>(true);
            _concealed = state.GetComponentLookup<Concealed>(true);
            _tall = state.GetComponentLookup<TallTarget>(true);
        }

        public void Update(ref SystemState state)
        {
            _stealthed.Update(ref state);
            _concealed.Update(ref state);
            _tall.Update(ref state);
        }

        /// <summary>Stealth first, then concealment.</summary>
        public readonly Cover Find(Entity entity)
        {
            if (_stealthed.HasEnabled(entity))
            {
                return Cover.Stealthed;
            }

            return _concealed.HasEnabled(entity) ? Cover.Concealed : Cover.None;
        }

        /// <summary>As tall as a vehicle or a building, so seen over low cover.</summary>
        public readonly bool IsTall(Entity entity) => _tall.HasComponent(entity);
    }
}
