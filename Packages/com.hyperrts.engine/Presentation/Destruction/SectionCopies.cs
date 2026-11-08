using HyperRTS.Presentation.Animation.Clips;
using HyperRTS.Presentation.Rendering.Parts;
using HyperRTS.Simulation.Destruction;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace HyperRTS.Presentation.Destruction
{
    /// <summary>
    /// The section models to copy in from their prefabs or remove this frame, gathered while the buildings are walked
    /// and applied after, since copying moves chunks. Each building lists its copies in <see cref="SectionCopy"/>.
    /// </summary>
    public struct SectionCopies
    {
        private struct Change
        {
            public Entity Building;
            public Entity Model;
            public bool Show;
            public bool Plays;
            public PlayingClip Clip;
        }

        private NativeList<Change> _changes;

        public static SectionCopies Create() => new() { _changes = new NativeList<Change>(Allocator.Temp) };

        /// <summary>Shows or removes <paramref name="model"/>'s copy on <paramref name="building"/>; no model does nothing.</summary>
        public void Set(Entity building, Entity model, bool show) => Add(building, model, show, false, default);

        /// <summary>Shows <paramref name="model"/>'s copy playing <paramref name="clip"/> from its start.</summary>
        public void Play(Entity building, Entity model, PlayingClip clip) => Add(building, model, true, true, clip);

        public void Apply(EntityManager em)
        {
            foreach (var change in _changes)
            {
                var copies = em.GetBuffer<SectionCopy>(change.Building);
                var index = Find(copies, change.Model);
                if (change.Show == index >= 0)
                {
                    continue;
                }

                if (change.Show)
                {
                    Make(em, change);
                }
                else
                {
                    var copy = copies[index].Copy;
                    copies.RemoveAtSwapBack(index);
                    Remove(em, change.Building, copy);
                }
            }

            _changes.Clear();
        }

        private void Add(Entity building, Entity model, bool show, bool plays, PlayingClip clip)
        {
            if (model != Entity.Null)
            {
                _changes.Add(new Change { Building = building, Model = model, Show = show, Plays = plays, Clip = clip });
            }
        }

        // Under its building with the building's hide reasons, and in its group so the building is destroyed whole.
        private static void Make(EntityManager em, in Change change)
        {
            var copy = em.Instantiate(change.Model);
            var bakedUnder = em.GetComponentData<Parent>(copy).Value;
            em.SetComponentData(copy, new Parent { Value = change.Building });

            // The copy draws this frame, before the transform systems place it: move it as far as the building is
            // from the building it was baked under, itself for a SubScene building.
            var moved = math.mul(em.GetComponentData<LocalToWorld>(change.Building).Value,
                math.inverse(em.GetComponentData<LocalToWorld>(bakedUnder).Value));
            var reasons = em.GetComponentData<HiddenBy>(change.Building).Reasons;
            var parts = em.GetBuffer<LinkedEntityGroup>(copy).Reinterpret<Entity>().ToNativeArray(Allocator.Temp);
            foreach (var part in parts)
            {
                if (em.HasComponent<LocalToWorld>(part))
                {
                    var place = em.GetComponentData<LocalToWorld>(part);
                    em.SetComponentData(part, new LocalToWorld { Value = math.mul(moved, place.Value) });
                }

                if (em.HasComponent<HiddenBy>(part))
                {
                    em.SetComponentData(part, new HiddenBy { Reasons = reasons });
                    if (em.HasComponent<MaterialMeshInfo>(part))
                    {
                        em.SetComponentEnabled<MaterialMeshInfo>(part, reasons == 0);
                    }
                }

                if (change.Plays && em.HasComponent<PlayingClip>(part))
                {
                    em.SetComponentData(part, change.Clip);
                }
            }

            em.GetBuffer<LinkedEntityGroup>(change.Building).AddRange(parts.Reinterpret<LinkedEntityGroup>());
            em.GetBuffer<SectionCopy>(change.Building).Add(new SectionCopy { Model = change.Model, Copy = copy });
        }

        private static void Remove(EntityManager em, Entity building, Entity copy)
        {
            var parts = em.GetBuffer<LinkedEntityGroup>(copy).Reinterpret<Entity>().ToNativeArray(Allocator.Temp);
            var group = em.GetBuffer<LinkedEntityGroup>(building);
            for (var i = group.Length - 1; i > 0; i--)
            {
                if (parts.Contains(group[i].Value))
                {
                    group.RemoveAtSwapBack(i);
                }
            }

            em.DestroyEntity(copy);
        }

        private static int Find(DynamicBuffer<SectionCopy> copies, Entity model)
        {
            for (var i = 0; i < copies.Length; i++)
            {
                if (copies[i].Model == model)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
