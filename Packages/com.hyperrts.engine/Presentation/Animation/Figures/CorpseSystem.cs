using System.Collections.Generic;
using HyperRTS.Presentation.Animation.Bones;
using HyperRTS.Presentation.Common;
using HyperRTS.Presentation.Rendering.Parts;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Infantry;
using HyperRTS.Simulation.Common.Players;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Collider = Unity.Physics.Collider;

namespace HyperRTS.Presentation.Animation.Figures
{
    /// <summary>
    /// Makes corpses of dying squad figures. Past its handover a figure near the camera with a free pose slot becomes a
    /// ragdoll drawn by copies of its renderers; the figures still on show when a squad dies fall as corpses too. Bodies
    /// freeze once they rest, and a corpse hides when its figure would have. Not Burst: it copies renderers and reads
    /// bone textures.
    /// </summary>
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(SquadFigureSystem))]
    [UpdateBefore(typeof(PoseSlotSystem))]
    public partial class CorpseSystem : SystemBase
    {
        private readonly Dictionary<BlobAssetReference<FigureClips>, BlobAssetReference<Collider>[]> _colliders = new();
        private RenderSwitch _switch;
        private CueVisibility _visibility;
        private Camera _camera;
        private EntityQuery _newSquads;
        private EntityQuery _goneSquads;
        private EntityQuery _ends;
        private EntityQuery _corpses;
        private NativeList<Handover> _handovers;
        private float _now;

        private struct Handover
        {
            public Entity Squad;
            public int Figure;
        }

        protected override void OnCreate()
        {
            RequireForUpdate<PoseSlots>();
            _switch = new RenderSwitch(ref CheckedStateRef);
            _visibility = new CueVisibility(ref CheckedStateRef);
            _newSquads = SystemAPI.QueryBuilder().WithAll<SquadFigures, EntityInfo, Owner>().WithNone<SquadEnd>().Build();
            _goneSquads = SystemAPI.QueryBuilder().WithAll<SquadEnd>().WithNone<SquadFigures>().Build();
            _ends = SystemAPI.QueryBuilder().WithAll<SquadEnd>().Build();
            _corpses = SystemAPI.QueryBuilder().WithAll<Corpse>().Build();
            _handovers = new NativeList<Handover>(Allocator.Persistent);
        }

        protected override void OnDestroy()
        {
            _handovers.Dispose();
            foreach (var colliders in _colliders.Values)
            {
                foreach (var collider in colliders)
                {
                    collider.Dispose();
                }
            }

            _colliders.Clear();
        }

        protected override void OnUpdate()
        {
            CompleteDependency();
            _now = (float)SystemAPI.Time.ElapsedTime;
            _camera = MainCamera.Find(_camera);
            EntityManager.AddComponent(_newSquads, new ComponentTypeSet(typeof(SquadEnd), typeof(ShownFigure)));
            Scan();
            MatchDeathCues();
            HandOverFigures();
            EndGoneSquads();
            UpdateCorpses();
        }

        // A live figure past its handover becomes a ragdoll at once, or keeps falling on its squad.
        private void HandOverFigures()
        {
            foreach (var handover in _handovers)
            {
                var figure = EntityManager.GetBuffer<SquadFigure>(handover.Squad)[handover.Figure];
                var member = EntityManager.GetBuffer<SquadMember>(handover.Squad)[handover.Figure];
                var world = EntityManager.GetComponentData<LocalToWorld>(figure.Figure).Value;
                ref var ragdoll = ref figure.Clips.Value.Ragdoll;
                var slots = SystemAPI.GetSingleton<PoseSlots>();
                if (!CanRagdoll(ref ragdoll, world.c3.xyz, slots) || !slots.TryTake(out var slot))
                {
                    continue;
                }

                var corpse = new Corpse
                {
                    Clips = figure.Clips,
                    World = world,
                    Throw = ragdoll.Tuning.Throw(member.Death, member.BlastDirection),
                    HandedOver = true,
                    HideAt = figure.HideAt,
                };
                var owner = EntityManager.GetComponentData<Owner>(handover.Squad).Value;
                var root = CreateCorpse(handover.Squad, figure.Figure, corpse, owner);
                StartRagdoll(root, slot);
                _switch.Update(ref CheckedStateRef);
                _switch.SetBranch(handover.Squad, figure.Figure, HideReason.Figure, true);
                figure.State = FigureState.Hidden;
                figure.StateSince = _now;
                var figures = EntityManager.GetBuffer<SquadFigure>(handover.Squad);
                figures[handover.Figure] = figure;
            }
        }

        private bool CanRagdoll(ref Ragdoll ragdoll, float3 position, in PoseSlots slots)
        {
            if (!ragdoll.HasBodies || _camera == null || !slots.HasFree)
            {
                return false;
            }

            return math.distance(_camera.transform.position, position) <= ragdoll.Tuning.cameraDistance;
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
            var slots = SystemAPI.GetSingleton<PoseSlots>();
            if (CanRagdoll(ref corpse.Clips.Value.Ragdoll, corpse.World.c3.xyz, slots) && slots.TryTake(out var slot))
            {
                StartRagdoll(root, slot);
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
            DestroyBodies(root);
            EntityManager.GetBuffer<CorpseBone>(root).Clear();
        }

        private void Hide(Entity root, in Corpse corpse)
        {
            if (corpse.Slot >= 0)
            {
                SystemAPI.GetSingleton<PoseSlots>().Release(corpse.Slot);
            }

            DestroyBodies(root);
            EntityManager.DestroyEntity(root);
        }

        private void DestroyBodies(Entity root)
        {
            var made = new NativeList<Entity>(Allocator.Temp);
            foreach (var body in EntityManager.GetBuffer<CorpseBody>(root))
            {
                made.Add(body.Body);
            }

            foreach (var joint in EntityManager.GetBuffer<CorpseJoint>(root))
            {
                made.Add(joint.Joint);
            }

            EntityManager.GetBuffer<CorpseBody>(root).Clear();
            EntityManager.GetBuffer<CorpseJoint>(root).Clear();
            EntityManager.DestroyEntity(made.AsArray());
        }
    }
}
