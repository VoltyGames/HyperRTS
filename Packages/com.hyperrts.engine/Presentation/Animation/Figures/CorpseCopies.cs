using HyperRTS.Presentation.Animation.Bones;
using HyperRTS.Presentation.Animation.Clips;
using HyperRTS.Presentation.Rendering;
using HyperRTS.Presentation.Rendering.LitTeam;
using HyperRTS.Simulation.Common.Infantry;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace HyperRTS.Presentation.Animation.Figures
{
    /// <summary>Makes corpses as copies of a figure's renderers, and the falls of figures whose squad is gone.</summary>
    internal sealed class CorpseCopies
    {
        /// <summary>Seconds a figure of a gone squad waits for its corpse before it hides with its squad.</summary>
        public const float FallWait = 0.1f;

        private readonly EntityManager _entities;
        private readonly CorpseBudget _budget;

        public CorpseCopies(EntityManager entities, CorpseBudget budget)
        {
            _entities = entities;
            _budget = budget;
        }

        // Nearest the camera first; what the budget leaves waits, and past the wait it is dropped.
        public void MakeWaitingFalls(float now)
        {
            _budget.SortWaiting();
            var waitingFalls = _budget.Waiting;
            var kept = 0;
            for (var i = 0; i < waitingFalls.Length; i++)
            {
                var waiting = waitingFalls[i];
                if (_budget.Left > 0)
                {
                    _budget.Left--;
                    Fall(waiting, now);
                }
                else if (now - waiting.Since <= FallWait)
                {
                    waitingFalls[kept++] = waiting;
                }
            }

            waitingFalls.Length = kept;
        }

        /// <summary>
        /// A corpse of <paramref name="model"/>'s renderers listed on <paramref name="source"/>, a squad or its prefab,
        /// copied to the corpse's world place in its owner's colours.
        /// </summary>
        public Entity Create(Entity source, Entity model, Corpse corpse, byte owner)
        {
            var parts = new NativeList<Entity>(Allocator.Temp);
            foreach (var renderer in _entities.GetBuffer<FigureRenderer>(source))
            {
                if (renderer.Model == model)
                {
                    parts.Add(renderer.Part);
                }
            }

            var root = _entities.CreateEntity(typeof(Corpse), typeof(CorpseBody), typeof(CorpseJoint), typeof(CorpseBone),
                typeof(LinkedEntityGroup));
            var copies = new NativeList<Entity>(Allocator.Temp) { root };
            foreach (var part in parts)
            {
                var copy = VisualPrefab.Copy(_entities, part);
                _entities.SetComponentData(copy, LocalTransform.FromMatrix(corpse.World));
                _entities.SetComponentData(copy, new LocalToWorld { Value = corpse.World });
                copies.Add(copy);
            }

            _entities.GetBuffer<LinkedEntityGroup>(root).AddRange(copies.AsArray().Reinterpret<LinkedEntityGroup>());
            corpse.Slot = -1;
            corpse.Reach = math.cmax(_entities.GetComponentData<RenderBounds>(copies[1]).Value.Extents);
            _entities.SetComponentData(root, corpse);
            _entities.AddComponentData(root, new TeamColorSource { Owner = owner });
            if (_entities.HasComponent<TeamStripBand>(source))
            {
                _entities.AddComponentData(root, _entities.GetComponentData<TeamStripBand>(source));
            }

            return root;
        }

        // The squad's own figures went with its ghost, so they wait to be copied from its prefab and fall on from where
        // they were.
        public void QueueShownFigures(Entity squad, Entity prefab, in SquadEnd end, float now)
        {
            using var shown = _entities.GetBuffer<ShownFigure>(squad).ToNativeArray(Allocator.Temp);
            using var figures = _entities.GetBuffer<SquadFigure>(prefab).ToNativeArray(Allocator.Temp);
            var dieHold = _entities.GetComponentData<SquadFigures>(prefab).DieHold;
            for (var i = 0; i < shown.Length; i++)
            {
                if (shown[i].Shown)
                {
                    _budget.Waiting.Add(new WaitingFall
                    {
                        Shown = shown[i], Prefab = prefab, Baked = figures[i], DieHold = dieHold, Owner = end.Owner,
                        Since = now,
                    });
                }
            }
        }

        // A living member's death went unseen, so it falls as if shot.
        private void Fall(in WaitingFall waiting, float now)
        {
            var shown = waiting.Shown;
            var baked = waiting.Baked;
            var model = shown.ShowsOther ? baked.Other.Model : new FigureModel { Figure = baked.Figure, Clips = baked.Clips };
            ref var clips = ref model.Clips.Value;
            var start = shown.Falling ? shown.FallStart : now;
            var death = shown.Falling ? shown.Death : DeathCause.Shot;
            var corpse = new Corpse
            {
                Clips = model.Clips,
                World = shown.World,
                Throw = clips.Ragdoll.Tuning.Throw(death, shown.BlastDirection),
                HandoverAt = start + clips.Ragdoll.Tuning.handoverTime,
                HandedOver = shown.Falling && shown.HandedOver,
                HideAt = shown.Falling ? shown.HideAt : now + waiting.DieHold,
            };
            var root = Create(waiting.Prefab, model.Figure, corpse, waiting.Owner);
            var direction = new float3(shown.BlastDirection.x, 0f, shown.BlastDirection.y);
            var fall = new PlayingClip { Value = clips.Fall(shown.Posture, death, direction, shown.World).Packed(start) };
            using var group = _entities.GetBuffer<LinkedEntityGroup>(root).ToNativeArray(Allocator.Temp);
            for (var i = 1; i < group.Length; i++)
            {
                var copy = group[i].Value;
                _entities.SetComponentData(copy, new LowerClip { Clip = fall });
                _entities.SetComponentData(copy, new UpperClip { Clip = fall });
                _entities.SetComponentData(copy, new LowerFade { Fade = ClipFade.None });
                _entities.SetComponentData(copy, new UpperFade { Fade = ClipFade.None });
            }
        }
    }
}
