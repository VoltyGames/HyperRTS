using HyperRTS.Simulation.Common;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Orders
{
    /// <summary>
    /// Reads this frame's <see cref="PlayerCommand"/>s of some types from players still in the match, and the owned
    /// entities each one addresses. Defeated players' commands are dropped here, so every command system agrees.
    /// </summary>
    public struct CommandReader
    {
        private TypeSet _types;
        private EntityQuery _players;
        private EntityQuery _selected;
        private ComponentLookup<Faction> _factions;

        /// <summary><paramref name="selected"/> matches what a command without its own subjects addresses.</summary>
        public CommandReader(ref SystemState state, in TypeSet types, EntityQuery selected)
            : this(ref state, types)
        {
            _selected = selected;
        }

        /// <summary>For player-level commands, which address no entities.</summary>
        public CommandReader(ref SystemState state, in TypeSet types)
        {
            _types = types;
            _selected = default;
            _factions = state.GetComponentLookup<Faction>(true);
            _players = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<Player, PlayerCommand, PlayerCommandSubject>().WithNone<Defeated>().Build(ref state);
        }

        /// <summary>
        /// This frame's commands of the reader's types, player by player in issue order. Reads only the command
        /// buffers, so callers can skip their job sync when it comes back empty.
        /// </summary>
        public NativeList<IssuedCommand> Read(ref SystemState state)
        {
            var issued = new NativeList<IssuedCommand>(Allocator.Temp);
            _factions.Update(ref state);
            foreach (var player in _players.ToEntityArray(Allocator.Temp))
            {
                Append(state.EntityManager, player, issued);
            }

            return issued;
        }

        /// <summary>This frame's commands of the reader's types from <paramref name="player"/> alone.</summary>
        public NativeList<IssuedCommand> Read(ref SystemState state, Entity player)
        {
            var issued = new NativeList<IssuedCommand>(Allocator.Temp);
            _factions.Update(ref state);
            if (_players.Matches(player))
            {
                Append(state.EntityManager, player, issued);
            }

            return issued;
        }

        /// <summary>The entities the command addresses that its player owns, in the order it lists them.</summary>
        public void Subjects(ref SystemState state, in IssuedCommand issued, NativeList<Entity> subjects)
        {
            var listed = state.EntityManager.GetBuffer<PlayerCommandSubject>(issued.Player, true);
            PlayerCommands.Collect(issued.Command, listed, _selected, subjects);
            var kept = 0;
            for (var i = 0; i < subjects.Length; i++)
            {
                var entity = subjects[i];
                if (IsOwned(entity, issued.Faction))
                {
                    subjects[kept++] = entity;
                }
            }

            subjects.Length = kept;
        }

        public NativeList<Entity> Subjects(ref SystemState state, in IssuedCommand issued)
        {
            var subjects = new NativeList<Entity>(Allocator.Temp);
            Subjects(ref state, issued, subjects);
            return subjects;
        }

        private readonly void Append(EntityManager entityManager, Entity player, NativeList<IssuedCommand> issued)
        {
            var faction = entityManager.GetComponentData<Player>(player).Faction;
            foreach (var command in entityManager.GetBuffer<PlayerCommand>(player, true))
            {
                if (_types.Contains(command.Type))
                {
                    issued.Add(new IssuedCommand { Player = player, Faction = faction, Command = command });
                }
            }
        }

        private readonly bool IsOwned(Entity entity, byte faction) =>
            _factions.TryGetComponent(entity, out var owner) && owner.Value == faction;
    }
}
