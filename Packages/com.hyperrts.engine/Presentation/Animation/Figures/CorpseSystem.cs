using HyperRTS.Presentation.Animation.Bones;
using HyperRTS.Presentation.Common;
using HyperRTS.Presentation.Rendering.Parts;
using HyperRTS.Simulation.Cues;
using HyperRTS.Simulation.Common.Prefabs;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Infantry;
using HyperRTS.Simulation.Common.Players;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;
using Unity.Transforms;
using UnityEngine;

namespace HyperRTS.Presentation.Animation.Figures
{
    /// <summary>
    /// Makes corpses of dying squad figures. Past its handover a figure near the camera with a free pose slot becomes a
    /// ragdoll drawn by copies of its renderers; the figures still on show when a squad dies fall as corpses too. A frame
    /// makes and hands over at most <see cref="EffectDetail.CorpsesPerFrameAt"/>, nearest the camera first. Bodies freeze
    /// once they rest, and a corpse hides when its figure would have. Not Burst: it copies renderers and reads bone
    /// textures.
    /// </summary>
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(SquadFigureSystem))]
    [UpdateBefore(typeof(PoseSlotSystem))]
    public partial class CorpseSystem : SystemBase
    {
        private static readonly ProfilerMarker MakeMarker = new("CorpseSystem.Make");

        // Seconds a gone squad waits for its death cue, which a remote client gets apart from the ghost's despawn.
        private const float CueWait = 0.5f;

        private readonly CorpseBudget _budget = new();
        private RenderSwitch _switch;
        private CorpseBodies _bodies;
        private CorpseCopies _copies;
        private CorpseDeathCues _deaths;
        private EntityQuery _newSquads;
        private EntityQuery _goneSquads;
        private EntityQuery _corpses;
        private float _now;

        protected override void OnCreate()
        {
            RequireForUpdate<PoseSlots>();
            _switch = new RenderSwitch(ref CheckedStateRef);
            _bodies = new CorpseBodies(EntityManager);
            _copies = new CorpseCopies(EntityManager, _budget);
            _deaths = new CorpseDeathCues(ref CheckedStateRef, EntityManager);
            _newSquads = SystemAPI.QueryBuilder().WithAll<SquadFigures, EntityInfo, Owner>().WithNone<SquadEnd>().Build();
            _goneSquads = SystemAPI.QueryBuilder().WithAll<SquadEnd>().WithNone<SquadFigures>().Build();
            _corpses = SystemAPI.QueryBuilder().WithAll<Corpse>().Build();
        }

        protected override void OnDestroy()
        {
            _budget.Dispose();
            _bodies.Dispose();
        }

        protected override void OnUpdate()
        {
            CompleteDependency();
            _now = (float)SystemAPI.Time.ElapsedTime;
            _budget.Begin();
            EntityManager.AddComponent(_newSquads, new ComponentTypeSet(typeof(SquadEnd), typeof(ShownFigure)));
            Scan();
            if (SystemAPI.TryGetSingletonBuffer<CueEvent>(out var cues, true))
            {
                _deaths.Match(cues);
            }

            EndGoneSquads();
            using (MakeMarker.Auto())
            {
                _copies.MakeWaitingFalls(_now);
            }

            HandOverFigures();
            UpdateCorpses();
        }

        private void Scan()
        {
            _budget.Handovers.Clear();
            new CorpseScanJob
            {
                Now = _now,
                Places = SystemAPI.GetComponentLookup<LocalToWorld>(true),
                Handovers = _budget.Handovers,
            }.Run();
        }

        // A ghost also goes when its squad walks into fog, so only one its death cue named leaves corpses.
        private void EndGoneSquads()
        {
            using var squads = _goneSquads.ToEntityArray(Allocator.Temp);
            foreach (var squad in squads)
            {
                var end = EntityManager.GetComponentData<SquadEnd>(squad);
                if (end.GoneAt < 0f && !end.Died)
                {
                    end.GoneAt = _now;
                    EntityManager.SetComponentData(squad, end);
                    continue;
                }

                if (end.Died)
                {
                    FallShownFigures(squad, end);
                }
                else if (_now < end.GoneAt + CueWait)
                {
                    continue;
                }

                EntityManager.RemoveComponent(squad, new ComponentTypeSet(typeof(SquadEnd), typeof(ShownFigure)));
            }
        }

        // The squad's own figures went with its ghost, so they wait to be copied from its prefab and fall on from where
        // they were.
        private void FallShownFigures(Entity squad, in SquadEnd end)
        {
            var prefab = SystemAPI.GetSingleton<PrefabRegistry>().Find(end.TypeId);
            if (prefab == Entity.Null)
            {
                Debug.LogError($"Squad corpses: no prefab registered for type id {end.TypeId}, so its last figures don't fall.");
                return;
            }

            _copies.QueueShownFigures(squad, prefab, end, _now);
        }

        // A live figure past its handover becomes a ragdoll at once, or keeps falling on its squad when the frame's
        // budget or the slots are spent.
        private void HandOverFigures()
        {
            _budget.SortHandovers(EntityManager);
            foreach (var handover in _budget.Handovers)
            {
                var figure = EntityManager.GetBuffer<SquadFigure>(handover.Squad)[handover.Figure];
                var member = EntityManager.GetBuffer<SquadMember>(handover.Squad)[handover.Figure];
                var world = handover.World;
                ref var ragdoll = ref figure.Clips.Value.Ragdoll;
                if (_budget.Left <= 0 || !TryTakeSlot(ref ragdoll, world.c3.xyz, out var slot))
                {
                    continue;
                }

                _budget.Left--;
                var corpse = new Corpse
                {
                    Clips = figure.Clips,
                    World = world,
                    Throw = ragdoll.Tuning.Throw(member.Death, member.BlastDirection),
                    HandedOver = true,
                    HideAt = figure.HideAt,
                };
                var owner = EntityManager.GetComponentData<Owner>(handover.Squad).Value;
                var root = _copies.Create(handover.Squad, figure.Figure, corpse, owner);
                _bodies.StartRagdoll(root, slot, SystemAPI.GetSingleton<PoseSlots>(), _now);
                _switch.Update(ref CheckedStateRef);
                _switch.SetBranch(handover.Squad, figure.Figure, HideReason.Figure, true);
                figure.State = FigureState.Hidden;
                figure.StateSince = _now;
                var figures = EntityManager.GetBuffer<SquadFigure>(handover.Squad);
                figures[handover.Figure] = figure;
            }
        }

        // A ragdoll needs bodies, the camera close enough and a free pose slot.
        private bool TryTakeSlot(ref Ragdoll ragdoll, float3 position, out int slot)
        {
            slot = -1;
            if (!ragdoll.HasBodies || _budget.Camera == null
                || math.distance(_budget.CameraPosition, position) > ragdoll.Tuning.cameraDistance)
            {
                return false;
            }

            return SystemAPI.GetSingleton<PoseSlots>().TryTake(out slot);
        }

        private void UpdateCorpses()
        {
            using var corpses = _corpses.ToEntityArray(Allocator.Temp);
            foreach (var root in corpses)
            {
                var corpse = EntityManager.GetComponentData<Corpse>(root);
                if (_now >= corpse.HideAt)
                {
                    Hide(root, corpse);
                }
                else if (!corpse.HandedOver && _now >= corpse.HandoverAt)
                {
                    HandOverCorpse(root, corpse);
                }
                else if (Settled(root, corpse))
                {
                    Freeze(root);
                }
            }
        }

        // A corpse made where its squad died plays its fall clip until its own handover.
        private void HandOverCorpse(Entity root, Corpse corpse)
        {
            corpse.HandedOver = true;
            EntityManager.SetComponentData(root, corpse);
            if (_budget.Left > 0 && TryTakeSlot(ref corpse.Clips.Value.Ragdoll, corpse.World.c3.xyz, out var slot))
            {
                _budget.Left--;
                _bodies.StartRagdoll(root, slot, SystemAPI.GetSingleton<PoseSlots>(), _now);
            }
        }

        private bool Settled(Entity root, in Corpse corpse)
        {
            if (EntityManager.GetBuffer<CorpseBody>(root).Length == 0)
            {
                return false;
            }

            var resting = corpse.Slow && _now - corpse.SlowSince >= corpse.Clips.Value.Ragdoll.Tuning.restTime;
            return resting || _now >= corpse.SettleAt;
        }

        // The slot keeps the last pose, so a lying body costs nothing until it hides.
        private void Freeze(Entity root)
        {
            _bodies.DestroyBodies(root);
            EntityManager.GetBuffer<CorpseBone>(root).Clear();
        }

        private void Hide(Entity root, in Corpse corpse)
        {
            if (corpse.Slot >= 0)
            {
                SystemAPI.GetSingleton<PoseSlots>().Release(corpse.Slot);
            }

            _bodies.DestroyBodies(root);
            EntityManager.DestroyEntity(root);
        }
    }
}
