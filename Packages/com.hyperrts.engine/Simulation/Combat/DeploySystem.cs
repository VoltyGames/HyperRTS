using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Selection;
using Unity.Burst;
using Unity.Entities;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>
    /// Handles Deploy and Pack Up commands for the commanded or selected <see cref="Deployable"/> units and moves each
    /// toward deployed or packed up. Deploying clears the unit's orders, so it halts where it stands.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(OrderSystemGroup))]
    public partial struct DeploySystem : ISystem
    {
        private CommandReader _commands;
        private OrderWriter _writer;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            var selected = SystemAPI.QueryBuilder().WithAll<Deployable, Selected, Faction>().WithNone<Dead>().Build();
            var types = TypeSet.Single(CommandType.Deploy).With(CommandType.PackUp);
            _commands = new CommandReader(ref state, types, selected);
            _writer = new OrderWriter(ref state);
            state.RequireForUpdate<Deployable>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            Command(ref state);
            new AdvanceJob { DeltaTime = SystemAPI.Time.DeltaTime }.ScheduleParallel();
        }

        private void Command(ref SystemState state)
        {
            var issued = _commands.Read(ref state);
            if (issued.IsEmpty)
            {
                return;
            }

            state.CompleteDependency();
            _writer.Update(ref state);
            foreach (var each in issued)
            {
                var deploy = each.Command.Type == CommandType.Deploy;
                foreach (var unit in _commands.Subjects(ref state, each))
                {
                    if (SystemAPI.HasComponent<Deployable>(unit))
                    {
                        Apply(ref state, unit, deploy);
                    }
                }
            }
        }

        private void Apply(ref SystemState state, Entity unit, bool deploy)
        {
            ref var deployable = ref SystemAPI.GetComponentRW<Deployable>(unit).ValueRW;
            if (!deploy)
            {
                deployable.PackUp();
                return;
            }

            if (deployable.Deploy())
            {
                _writer.Stop(unit);
            }
        }

        [BurstCompile]
        [WithNone(typeof(Dead))]
        private partial struct AdvanceJob : IJobEntity
        {
            public float DeltaTime;

            private void Execute(ref Deployable deployable) => deployable.Advance(DeltaTime);
        }
    }
}
