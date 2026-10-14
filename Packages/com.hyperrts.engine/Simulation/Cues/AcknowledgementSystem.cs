using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Selection;
using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Simulation.Cues
{
    /// <summary>
    /// Voice acknowledgements: when the local player selects units or orders them to move or attack, one of them
    /// answers with its select, move or attack cue. Client-side only (never sent), and rate-limited so spam-clicking
    /// doesn't stack voices.
    /// </summary>
    // After the OrderFirst input systems, before the OrderLast systems that send or clear this frame's commands.
    [BurstCompile]
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(OrderSystemGroup))]
    public partial struct AcknowledgementSystem : ISystem
    {
        /// <summary>Seconds between two acknowledgements.</summary>
        public const float Interval = 1f;

        private EntityQuery _selected;
        private CueWriter _cues;
        private double _next;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _selected = SystemAPI.QueryBuilder().WithAll<Selected, Faction, LocalTransform, EntitySound>().Build();
            _cues = new CueWriter(ref state);
            state.RequireForUpdate<LocalPlayer>();
            state.RequireForUpdate<CueQueue>();
            state.RequireForUpdate<FactionRelations>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var now = SystemAPI.Time.ElapsedTime;
            if (now < _next)
            {
                return;
            }

            var player = SystemAPI.GetSingletonEntity<LocalPlayer>();
            var faction = SystemAPI.GetComponent<Player>(player).Faction;
            var slot = Requested(ref state, player, faction, out var command);
            if (slot == CueSlot.None)
            {
                return;
            }

            state.CompleteDependency();
            _cues.Update(ref state, SystemAPI.GetSingletonEntity<CueQueue>());
            var listed = SystemAPI.GetBuffer<PlayerCommandSubject>(player);
            foreach (var unit in PlayerCommands.Collect(command, listed, _selected))
            {
                if (TryAnswer(ref state, unit, slot, faction))
                {
                    _next = now + Interval;
                    return;
                }
            }
        }

        /// <summary>The voice this frame's input asks for; a selection gesture reads as a command on the selection.</summary>
        private CueSlot Requested(ref SystemState state, Entity player, byte faction, out PlayerCommand command)
        {
            var slot = CueSlot.None;
            command = default;
            foreach (var issued in SystemAPI.GetBuffer<PlayerCommand>(player))
            {
                var answer = Answer(ref state, issued, faction);
                if (answer != CueSlot.None)
                {
                    slot = answer;
                    command = issued;
                }
            }

            if (slot != CueSlot.None || !SystemAPI.TryGetSingleton(out SelectionInput input))
            {
                return slot;
            }

            return IsSelecting(input.Command) ? CueSlot.Select : CueSlot.None;
        }

        private CueSlot Answer(ref SystemState state, in PlayerCommand command, byte faction)
        {
            switch (command.Type)
            {
                case CommandType.Move:
                case CommandType.ReturnToBase:
                    return CueSlot.Move;
                case CommandType.Attack:
                case CommandType.AttackMove:
                case CommandType.Patrol:
                case CommandType.Escort:
                    return CueSlot.Attack;
                case CommandType.Smart:
                    return IsHostile(ref state, command.Target, faction) ? CueSlot.Attack : CueSlot.Move;
                default:
                    return CueSlot.None;
            }
        }

        private bool IsHostile(ref SystemState state, Entity target, byte faction)
        {
            if (!SystemAPI.HasComponent<Faction>(target))
            {
                return false;
            }

            var relations = SystemAPI.GetSingleton<FactionRelations>();
            return relations.IsHostile(faction, SystemAPI.GetComponent<Faction>(target).Value);
        }

        private static bool IsSelecting(SelectionCommand gesture) =>
            gesture != SelectionCommand.None && gesture != SelectionCommand.AssignGroup;

        private bool TryAnswer(ref SystemState state, Entity unit, CueSlot slot, byte faction)
        {
            if (!SystemAPI.HasComponent<Faction>(unit) || SystemAPI.GetComponent<Faction>(unit).Value != faction)
            {
                return false;
            }

            var transform = SystemAPI.GetComponent<LocalTransform>(unit);
            return _cues.Play(unit, slot, transform, transform.Forward(), faction);
        }
    }
}
