using HyperRTS.Presentation.Animation.Bones;
using HyperRTS.Presentation.Animation.Clips;
using HyperRTS.Presentation.Rendering;
using HyperRTS.Presentation.Rendering.LitTeam;
using HyperRTS.Simulation.Common.Infantry;
using HyperRTS.Simulation.Common.Prefabs;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

namespace HyperRTS.Presentation.Animation.Figures
{
    public partial class CorpseSystem
    {
        /// <summary>
        /// A corpse of <paramref name="model"/>'s renderers listed on <paramref name="source"/>, a squad or its prefab,
        /// copied to the corpse's world place in its owner's colours.
        /// </summary>
        private Entity CreateCorpse(Entity source, Entity model, Corpse corpse, byte owner)
        {
            var parts = new NativeList<Entity>(Allocator.Temp);
            foreach (var renderer in EntityManager.GetBuffer<FigureRenderer>(source))
            {
                if (renderer.Model == model)
                {
                    parts.Add(renderer.Part);
                }
            }

            var root = EntityManager.CreateEntity(typeof(Corpse), typeof(CorpseBody), typeof(CorpseJoint), typeof(CorpseBone),
                typeof(LinkedEntityGroup));
            var copies = new NativeList<Entity>(Allocator.Temp) { root };
            foreach (var part in parts)
            {
                var copy = VisualPrefab.Copy(EntityManager, part);
                EntityManager.SetComponentData(copy, LocalTransform.FromMatrix(corpse.World));
                EntityManager.SetComponentData(copy, new LocalToWorld { Value = corpse.World });
                copies.Add(copy);
            }

            EntityManager.GetBuffer<LinkedEntityGroup>(root).AddRange(copies.AsArray().Reinterpret<LinkedEntityGroup>());
            corpse.Slot = -1;
            corpse.Reach = math.cmax(EntityManager.GetComponentData<RenderBounds>(copies[1]).Value.Extents);
            EntityManager.SetComponentData(root, corpse);
            EntityManager.AddComponentData(root, new TeamColorSource { Owner = owner });
            if (EntityManager.HasComponent<TeamStripBand>(source))
            {
                EntityManager.AddComponentData(root, EntityManager.GetComponentData<TeamStripBand>(source));
            }

            return root;
        }

        // The squad's own figures went with its ghost, so they are copied from its prefab and fall on from where they were.
        private void FallShownFigures(Entity squad, in SquadEnd end)
        {
            var prefab = SystemAPI.GetSingleton<PrefabRegistry>().Find(end.TypeId);
            if (prefab == Entity.Null)
            {
                Debug.LogError($"Squad corpses: no prefab registered for type id {end.TypeId}, so its last figures don't fall.");
                return;
            }

            using var shown = EntityManager.GetBuffer<ShownFigure>(squad).ToNativeArray(Allocator.Temp);
            using var figures = EntityManager.GetBuffer<SquadFigure>(prefab).ToNativeArray(Allocator.Temp);
            var dieHold = EntityManager.GetComponentData<SquadFigures>(prefab).DieHold;
            for (var i = 0; i < shown.Length; i++)
            {
                if (shown[i].Shown)
                {
                    Fall(shown[i], prefab, figures[i], dieHold, end.Owner);
                }
            }
        }

        // A living member's death went unseen, so it falls as if shot.
        private void Fall(in ShownFigure shown, Entity prefab, in SquadFigure baked, float dieHold, byte owner)
        {
            var model = shown.ShowsOther ? baked.Other.Model : new FigureModel { Figure = baked.Figure, Clips = baked.Clips };
            ref var clips = ref model.Clips.Value;
            var start = shown.Falling ? shown.FallStart : _now;
            var death = shown.Falling ? shown.Death : DeathCause.Shot;
            var corpse = new Corpse
            {
                Clips = model.Clips,
                World = shown.World,
                Throw = clips.Ragdoll.Tuning.Throw(death, shown.BlastDirection),
                HandoverAt = start + clips.Ragdoll.Tuning.handoverTime,
                HandedOver = shown.Falling && shown.HandedOver,
                HideAt = shown.Falling ? shown.HideAt : _now + dieHold,
            };
            var root = CreateCorpse(prefab, model.Figure, corpse, owner);
            var direction = new float3(shown.BlastDirection.x, 0f, shown.BlastDirection.y);
            var fall = new PlayingClip { Value = clips.Fall(shown.Posture, death, direction, shown.World).Packed(start) };
            using var group = EntityManager.GetBuffer<LinkedEntityGroup>(root).ToNativeArray(Allocator.Temp);
            for (var i = 1; i < group.Length; i++)
            {
                var copy = group[i].Value;
                EntityManager.SetComponentData(copy, new LowerClip { Clip = fall });
                EntityManager.SetComponentData(copy, new UpperClip { Clip = fall });
                EntityManager.SetComponentData(copy, new LowerFade { Fade = ClipFade.None });
                EntityManager.SetComponentData(copy, new UpperFade { Fade = ClipFade.None });
            }
        }
    }
}
