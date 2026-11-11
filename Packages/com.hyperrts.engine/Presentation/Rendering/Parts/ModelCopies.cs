using HyperRTS.Presentation.Animation.Clips;
using HyperRTS.Presentation.Construction;
using HyperRTS.Presentation.Fog;
using HyperRTS.Presentation.Rendering.LitTeam;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace HyperRTS.Presentation.Rendering.Parts
{
    /// <summary>
    /// The model prefabs to copy into their roots or remove this frame, gathered while the roots are walked and applied
    /// after, since copying moves chunks. <see cref="ModelCopyBakingSystem"/> makes the prefabs; each root lists its
    /// copies in <see cref="ModelCopy"/>.
    /// </summary>
    public struct ModelCopies
    {
        private struct Change
        {
            public Entity Root;
            public Entity Model;
            public bool Show;
            public bool Plays;
            public PlayingClip Clip;
        }

        private NativeList<Change> _changes;

        public static ModelCopies Create() => new() { _changes = new NativeList<Change>(Allocator.Temp) };

        /// <summary>Shows or removes <paramref name="model"/>'s copy in <paramref name="root"/>; no model does nothing.</summary>
        public void Set(Entity root, Entity model, bool show) => Add(root, model, show, false, default);

        /// <summary>Shows <paramref name="model"/>'s copy playing <paramref name="clip"/> from its start.</summary>
        public void Play(Entity root, Entity model, PlayingClip clip) => Add(root, model, true, true, clip);

        public void Apply(EntityManager em)
        {
            foreach (var change in _changes)
            {
                var copies = em.GetBuffer<ModelCopy>(change.Root);
                var index = CopyIndex(copies, change.Model);
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
                    Remove(em, change.Root, copy);
                }
            }

            _changes.Clear();
        }

        private void Add(Entity root, Entity model, bool show, bool plays, PlayingClip clip)
        {
            if (model != Entity.Null)
            {
                _changes.Add(new Change { Root = root, Model = model, Show = show, Plays = plays, Clip = clip });
            }
        }

        // Where the model was baked, with the root's hide reasons and paint, and in its group so it is destroyed whole.
        private static void Make(EntityManager em, in Change change)
        {
            var copy = em.Instantiate(change.Model);
            var bakedUnder = em.GetComponentData<Parent>(copy).Value;
            var parent = LiveParent(em, change.Root, bakedUnder);
            em.SetComponentData(copy, new Parent { Value = parent });

            // The copy draws this frame, before the transform systems place it: move it as far as its parent is from
            // the one it was baked under, the same entity in a SubScene object.
            var moved = math.mul(em.GetComponentData<LocalToWorld>(parent).Value,
                math.inverse(em.GetComponentData<LocalToWorld>(bakedUnder).Value));
            // Only gameplay roots carry hide reasons; a tree has none.
            var reasons = em.HasComponent<HiddenBy>(change.Root)
                ? em.GetComponentData<HiddenBy>(change.Root).Reasons
                : default;
            var painted = FirstMesh(em, change.Root);
            var parts = em.GetBuffer<LinkedEntityGroup>(copy).Reinterpret<Entity>().ToNativeArray(Allocator.Temp);
            foreach (var part in parts)
            {
                Place(em, part, moved, reasons, painted, change);
            }

            em.GetBuffer<LinkedEntityGroup>(change.Root).AddRange(parts.Reinterpret<LinkedEntityGroup>());
            em.GetBuffer<ModelCopy>(change.Root).Add(new ModelCopy { Model = change.Model, Copy = copy });
        }

        private static void Place(EntityManager em, Entity part, in float4x4 moved, HideReason reasons, Entity painted,
            in Change change)
        {
            if (em.HasComponent<LocalToWorld>(part))
            {
                var place = em.GetComponentData<LocalToWorld>(part);
                em.SetComponentData(part, new LocalToWorld { Value = math.mul(moved, place.Value) });
            }

            if (em.HasComponent<HiddenBy>(part))
            {
                em.SetComponentData(part, new HiddenBy { Reasons = reasons });
            }

            if (em.HasComponent<MaterialMeshInfo>(part))
            {
                em.SetComponentEnabled<MaterialMeshInfo>(part, reasons == 0);
                Paint(em, painted, part);
            }

            if (change.Plays && em.HasComponent<PlayingClip>(part))
            {
                em.SetComponentData(part, change.Clip);
            }
        }

        /// <summary>
        /// The root's own entity for <paramref name="bakedUnder"/>: itself in a SubScene object, and the same place in
        /// the group of an instance, since instancing keeps the prefab's group order and copies only add to its end.
        /// </summary>
        private static Entity LiveParent(EntityManager em, Entity root, Entity bakedUnder)
        {
            if (!em.HasComponent<Prefab>(bakedUnder))
            {
                return bakedUnder;
            }

            var prefab = bakedUnder;
            while (em.HasComponent<Parent>(prefab))
            {
                prefab = em.GetComponentData<Parent>(prefab).Value;
            }

            var index = em.GetBuffer<LinkedEntityGroup>(prefab).Reinterpret<Entity>().AsNativeArray().IndexOf(bakedUnder);
            return em.GetBuffer<LinkedEntityGroup>(root)[index].Value;
        }

        // The root's group lists its own parts before any copy, so its first mesh carries what painters put on it.
        private static Entity FirstMesh(EntityManager em, Entity root)
        {
            foreach (var part in em.GetBuffer<LinkedEntityGroup>(root))
            {
                if (em.HasComponent<MaterialMeshInfo>(part.Value))
                {
                    return part.Value;
                }
            }

            return Entity.Null;
        }

        /// <summary>
        /// The per-instance values painters put on every mesh of a root once (team colour, fog fade, lights, build
        /// progress, grime), which a later part would otherwise miss until the next change.
        /// </summary>
        private static void Paint(EntityManager em, Entity from, Entity part)
        {
            if (from == Entity.Null)
            {
                return;
            }

            Copy<TeamColor>(em, from, part);
            Copy<TeamStrip>(em, from, part);
            Copy<RevealFade>(em, from, part);
            Copy<LightsOn>(em, from, part);
            Copy<BuildProgress>(em, from, part);
            Copy<GrimeLevels>(em, from, part);
            Copy<GrimeBand>(em, from, part);
        }

        private static void Copy<T>(EntityManager em, Entity from, Entity part) where T : unmanaged, IComponentData
        {
            if (em.HasComponent<T>(from))
            {
                em.AddComponentData(part, em.GetComponentData<T>(from));
            }
        }

        private static void Remove(EntityManager em, Entity root, Entity copy)
        {
            var parts = em.GetBuffer<LinkedEntityGroup>(copy).Reinterpret<Entity>().ToNativeArray(Allocator.Temp);
            var group = em.GetBuffer<LinkedEntityGroup>(root);
            for (var i = group.Length - 1; i > 0; i--)
            {
                if (parts.Contains(group[i].Value))
                {
                    group.RemoveAtSwapBack(i);
                }
            }

            em.DestroyEntity(copy);
        }

        /// <summary>The copy of <paramref name="model"/> showing in <paramref name="root"/>, or none.</summary>
        public static Entity Find(EntityManager em, Entity root, Entity model)
        {
            var copies = em.GetBuffer<ModelCopy>(root, true);
            var index = CopyIndex(copies, model);
            return index >= 0 ? copies[index].Copy : Entity.Null;
        }

        private static int CopyIndex(DynamicBuffer<ModelCopy> copies, Entity model)
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
