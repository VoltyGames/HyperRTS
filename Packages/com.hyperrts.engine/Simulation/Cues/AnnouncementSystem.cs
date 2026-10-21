using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Cues
{
    /// <summary>
    /// Turns the frame's gameplay events into announcer alerts for the players they concern: a unit lost, a building
    /// hit, a building, unit or research finished. Low power is announced by the power module, through its own gate.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(LifecycleSystemGroup))]
    [UpdateAfter(typeof(DeathCueSystem))]
    public partial struct AnnouncementSystem : ISystem
    {
        private CueWriter _cues;
        private AnnouncementGate _gate;
        private ComponentLookup<EntityInfo> _info;
        private ComponentLookup<LocalTransform> _transforms;
        private ComponentLookup<BuildingTag> _buildings;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _cues = new CueWriter(ref state);
            _gate = new AnnouncementGate(Allocator.Persistent);
            _info = state.GetComponentLookup<EntityInfo>(true);
            _transforms = state.GetComponentLookup<LocalTransform>(true);
            _buildings = state.GetComponentLookup<BuildingTag>(true);
            state.RequireForUpdate<CueQueue>();
            // Without an announcer nobody hears alerts, so none are sent.
            state.RequireForUpdate(state.GetEntityQuery(ComponentType.ReadOnly<AnnouncerVoice>()));
            state.RequireForUpdate<GameEvents>();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state) => _gate.Dispose();

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _cues.Update(ref state, SystemAPI.GetSingletonEntity<CueQueue>());
            _info.Update(ref state);
            _transforms.Update(ref state);
            _buildings.Update(ref state);
            var now = SystemAPI.Time.ElapsedTime;
            new EventsJob
            {
                Now = now, Cues = _cues, Gate = _gate, Info = _info, Transforms = _transforms, Buildings = _buildings,
            }.Schedule();
            new LostJob { Now = now, Cues = _cues, Gate = _gate }.Schedule();
        }

        // Single-threaded, like the next job: both append to the one cue queue and share the gate.
        [BurstCompile]
        [WithAll(typeof(GameEvents))]
        private partial struct EventsJob : IJobEntity
        {
            public double Now;
            public CueWriter Cues;
            public AnnouncementGate Gate;
            [ReadOnly] public ComponentLookup<EntityInfo> Info;
            [ReadOnly] public ComponentLookup<LocalTransform> Transforms;
            [ReadOnly] public ComponentLookup<BuildingTag> Buildings;

            private void Execute(in DynamicBuffer<UnitProduced> produced, in DynamicBuffer<BuildingCompleted> built,
                in DynamicBuffer<ResearchCompleted> researched, in DynamicBuffer<EntityDamaged> damaged)
            {
                foreach (var unit in produced)
                {
                    Announce(AnnouncementKind.UnitReady, unit.Faction, unit.Position, unit.Prefab);
                }

                foreach (var building in built)
                {
                    var position = Transforms[building.Building].Position;
                    Announce(AnnouncementKind.ConstructionComplete, building.Faction, position, building.Building);
                }

                foreach (var research in researched)
                {
                    var position = Transforms[research.Producer].Position;
                    Announce(AnnouncementKind.UpgradeComplete, research.Faction, position, research.Prefab);
                }

                foreach (var hit in damaged)
                {
                    if (Buildings.HasComponent(hit.Entity))
                    {
                        var position = Transforms[hit.Entity].Position;
                        Announce(AnnouncementKind.BaseUnderAttack, hit.Faction, position, hit.Entity);
                    }
                }
            }

            private void Announce(AnnouncementKind kind, byte faction, float3 position, Entity subject)
            {
                if (Gate.TryPass(kind, faction, Now))
                {
                    var typeId = Info.TryGetComponent(subject, out var info) ? info.TypeId : 0;
                    Cues.Announce(kind, faction, position, typeId);
                }
            }
        }

        [BurstCompile]
        [WithAll(typeof(Dead), typeof(UnitTag))]
        private partial struct LostJob : IJobEntity
        {
            public double Now;
            public CueWriter Cues;
            public AnnouncementGate Gate;

            private void Execute(in LocalTransform transform, in Faction faction, in EntityInfo info)
            {
                var owned = faction.Value != Faction.Neutral;
                if (owned && Gate.TryPass(AnnouncementKind.UnitLost, faction.Value, Now))
                {
                    Cues.Announce(AnnouncementKind.UnitLost, faction.Value, transform.Position, info.TypeId);
                }
            }
        }
    }
}
