using HyperRTS.Core;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Selection;
using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Simulation.Audio
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
        private SoundWriter _sounds;
        private double _next;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _selected = SystemAPI.QueryBuilder().WithAll<Selected, Faction, LocalTransform, EntitySound>().Build();
            _sounds = new SoundWriter(ref state);
            state.RequireForUpdate<LocalPlayer>();
            state.RequireForUpdate<SoundQueue>();
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
            if (slot == SoundSlot.None)
            {
                return;
            }

            state.CompleteDependency();
            _sounds.Update(ref state, SystemAPI.GetSingletonEntity<SoundQueue>());
            var listed = SystemAPI.GetBuffer<PlayerCommandSubject>(player);
            foreach (var unit in CommandSubjects.Collect(command, listed, _selected))
            {
                if (TryAnswer(ref state, unit, slot, faction))
                {
                    _next = now + Interval;
                    return;
                }
            }
        }

        /// <summary>The voice this frame's input asks for; a selection gesture reads as a command on the selection.</summary>
        private SoundSlot Requested(ref SystemState state, Entity player, byte faction, out PlayerCommand command)
        {
            var slot = SoundSlot.None;
            command = default;
            foreach (var issued in SystemAPI.GetBuffer<PlayerCommand>(player))
            {
                var answer = Answer(ref state, issued, faction);
                if (answer != SoundSlot.None)
                {
                    slot = answer;
                    command = issued;
                }
            }

            if (slot != SoundSlot.None || !SystemAPI.TryGetSingleton(out SelectionInput input))
            {
                return slot;
            }

            return IsSelecting(input.Command) ? SoundSlot.Select : SoundSlot.None;
        }

        private SoundSlot Answer(ref SystemState state, in PlayerCommand command, byte faction)
        {
            switch (command.Type)
            {
                case CommandType.Move:
                case CommandType.ReturnToBase:
                    return SoundSlot.Move;
                case CommandType.Attack:
                case CommandType.AttackMove:
                case CommandType.Patrol:
                case CommandType.Escort:
                    return SoundSlot.Attack;
                case CommandType.Smart:
                    return IsHostile(ref state, command.Target, faction) ? SoundSlot.Attack : SoundSlot.Move;
                default:
                    return SoundSlot.None;
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

        private bool TryAnswer(ref SystemState state, Entity unit, SoundSlot slot, byte faction)
        {
            if (!SystemAPI.HasComponent<Faction>(unit) || SystemAPI.GetComponent<Faction>(unit).Value != faction)
            {
                return false;
            }

            var typeId = _sounds.TypeIdFor(unit, slot);
            if (typeId == 0)
            {
                return false;
            }

            _sounds.Add(typeId, slot, SystemAPI.GetComponent<LocalTransform>(unit).Position, faction);
            return true;
        }
    }
}
