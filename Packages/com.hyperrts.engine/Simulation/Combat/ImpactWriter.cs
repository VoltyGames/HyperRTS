using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Cues;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>
    /// Appends to the <see cref="ImpactQueue"/> from one job thread; shooters without marks or impact effects write
    /// nothing.
    /// </summary>
    public struct ImpactWriter
    {
        private BufferLookup<ImpactEvent> _events;
        [ReadOnly] private BufferLookup<EntityImpactMark> _marks;
        [ReadOnly] private ComponentLookup<EntityImpactEffects> _effects;
        [ReadOnly] private ComponentLookup<EntityInfo> _info;
        private Entity _queue;

        public ImpactWriter(ref SystemState state) : this()
        {
            _events = state.GetBufferLookup<ImpactEvent>();
            _marks = state.GetBufferLookup<EntityImpactMark>(true);
            _effects = state.GetComponentLookup<EntityImpactEffects>(true);
            _info = state.GetComponentLookup<EntityInfo>(true);
        }

        /// <summary>
        /// Call each update before scheduling; the queue exists once <see cref="ImpactClearSystem"/> is created.
        /// </summary>
        public void Update(ref SystemState state, Entity queue)
        {
            _events.Update(ref state);
            _marks.Update(ref state);
            _effects.Update(ref state);
            _info.Update(ref state);
            _queue = queue;
        }

        /// <summary><paramref name="shooter"/>'s type id if its shots leave marks or effects, else 0.</summary>
        public int TypeIdFor(Entity shooter)
        {
            var marked = _marks.TryGetBuffer(shooter, out var marks) && !marks.IsEmpty;
            if (!marked && !_effects.HasComponent(shooter))
            {
                return 0;
            }

            return _info.TryGetComponent(shooter, out var info) ? info.TypeId : 0;
        }

        /// <summary>
        /// Reports where <paramref name="hit"/> landed for a shooter type; a type id of 0 is ignored.
        /// </summary>
        public void Add(int typeId, in DamageEvent hit)
        {
            if (typeId == 0)
            {
                return;
            }

            _events[_queue].Add(new ImpactEvent
            {
                TypeId = typeId,
                Position = hit.Position,
                Radius = hit.Radius,
                Faction = hit.SourceFaction,
                Target = hit.Target,
            });
        }
    }
}
