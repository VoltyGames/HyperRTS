using HyperRTS.Presentation.Animation.Bones;
using HyperRTS.Presentation.Animation.Clips;
using HyperRTS.Presentation.Rendering.Parts;
using HyperRTS.Simulation.Combat.Deploy;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common.Systems;
using HyperRTS.Simulation.Common.Infantry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace HyperRTS.Presentation.Animation.Figures
{
    /// <summary>
    /// Plays each <see cref="SquadFigure"/>'s renderers the clips for its motion, posture and weapon; a dead member's
    /// figure dies, then hides. <see cref="SquadFigurePlacementSystem"/> places them.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(EntitiesGraphicsSystem))]
    public partial struct SquadFigureSystem : ISystem
    {
        private RenderSwitch _switch;
        private EntityQuery _parts;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _switch = new RenderSwitch(ref state);
            _parts = SystemAPI.QueryBuilder().WithAll<FigurePart, LowerClip, UpperClip, LowerFade, UpperFade>().Build();
            state.RequireForUpdate<SquadFigures>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _switch.Update(ref state);
            var now = (float)SystemAPI.Time.ElapsedTime;
            var hides = new NativeQueue<FigureHide>(state.WorldUpdateAllocator);
            var squads = new SquadFigureJob
            {
                Now = now,
                DeltaTime = SystemAPI.Time.DeltaTime,
                Poses = SystemAPI.GetBufferLookup<WeaponPose>(true),
                Deployables = SystemAPI.GetComponentLookup<Deployable>(true),
                Places = SystemAPI.GetComponentLookup<LocalToWorld>(true),
                Hides = hides.AsParallelWriter(),
            }.ScheduleParallel(state.Dependency);
            var hide = new SquadFigureHideJob { Hides = hides, Switch = _switch }.Schedule(squads);
            var parts = new SquadFigureClipJob
            {
                Now = now,
                Figures = SystemAPI.GetBufferLookup<SquadFigure>(true),
                Parts = SystemAPI.GetComponentTypeHandle<FigurePart>(),
                Lowers = SystemAPI.GetComponentTypeHandle<LowerClip>(),
                Uppers = SystemAPI.GetComponentTypeHandle<UpperClip>(),
                LowerFades = SystemAPI.GetComponentTypeHandle<LowerFade>(),
                UpperFades = SystemAPI.GetComponentTypeHandle<UpperFade>(),
            }.ScheduleParallel(_parts, squads);
            state.Dependency = JobHandle.CombineDependencies(hide, parts);
        }
    }
}
