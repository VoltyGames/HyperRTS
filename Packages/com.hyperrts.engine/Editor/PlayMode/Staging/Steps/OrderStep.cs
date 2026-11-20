using System;
using System.Collections.Generic;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Orders;
using Newtonsoft.Json.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Editor.PlayMode.Staging.Steps
{
    /// <summary>Gives a group a command through its player, the way input and AI do.</summary>
    internal sealed class OrderStep : StagingStep
    {
        public string units;
        public float[] move;
        public string attack;
        public float[] attackGround;
        public string ability;
        public JToken command;
        public bool? stop;
        public int? argument;
        public float[] at;
        public string target;

        private CommandType _type;
        private float2? _point;
        private string _target;
        private int _argument;

        public override IEnumerable<string> Uses => _target == null ? new[] { units } : new[] { units, _target };

        public override void Check()
        {
            Need(units, "units");
            var action = OneOf(
                ("move", move != null),
                ("attack", attack != null),
                ("attackGround", attackGround != null),
                ("ability", ability != null),
                ("command", command != null),
                ("stop", stop != null));
            RejectExtras(action);
            switch (action)
            {
                case "move":
                    _type = CommandType.Move;
                    _point = Point(move, action);
                    return;
                case "attackGround":
                    _type = CommandType.FireMission;
                    _point = Point(attackGround, action);
                    return;
                case "attack":
                    _type = CommandType.Attack;
                    _target = attack;
                    return;
                case "stop":
                    _type = stop == true ? CommandType.Stop : throw new InvalidOperationException("has 'stop' false; leave the key out instead");
                    return;
            }

            // Abilities go by the hash of their name, as the command card sends them.
            var isAbility = action == "ability";
            _type = isAbility ? CommandType.UseAbility : ReadCommand();
            _argument = isAbility ? EntityInfo.TypeIdFromName(ability) : argument ?? 0;
            _point = at == null ? null : Point(at, "at");
            _target = target;
        }

        // Only abilities and raw commands take a point, a target or an argument beside their action.
        private void RejectExtras(string action)
        {
            var hasExtras = at != null || target != null || argument != null;
            if (hasExtras && action is not ("ability" or "command"))
            {
                throw new InvalidOperationException($"has 'at', 'target' or 'argument', which '{action}' doesn't take");
            }
        }

        public override void Run(StagingRun run)
        {
            var entityManager = run.World;
            var live = run.LiveUnits(units, "order");

            var player = run.FindPlayer(run.FindGroup(units).Owner);
            var command = new PlayerCommand { Type = _type, Argument = _argument };
            if (_point.HasValue)
            {
                command.Position = run.GroundPoint(_point.Value);
            }

            if (_target == null)
            {
                using var listed = new NativeArray<Entity>(live.ToArray(), Allocator.Temp);
                PlayerCommands.Address(ref command, entityManager.GetBuffer<PlayerCommandSubject>(player), listed);
                entityManager.GetBuffer<PlayerCommand>(player).Add(command);
                return;
            }

            // The target lookup can change the world's structure, which drops a buffer taken before it.
            var targets = run.LiveUnits(_target, "target");
            var commands = entityManager.GetBuffer<PlayerCommand>(player);

            for (var i = 0; i < live.Count; i++)
            {
                var each = command;
                each.Unit = live[i];
                each.Target = targets[i % targets.Count];
                commands.Add(each);
            }
        }

        // A name of an engine command, or the number of a game's own.
        private CommandType ReadCommand()
        {
            var advice = $"give a CommandType name or a game command number from {(int)CommandType.GameFirst}";
            if (command.Type is JTokenType.Integer or JTokenType.Float)
            {
                var number = (int)command;
                return IsGameCommand(number) ? (CommandType)number : throw new InvalidOperationException($"has a 'command' of {number}: {advice}");
            }

            var name = command.Type == JTokenType.String ? (string)command : string.Empty;
            var parsed = CommandType.None;
            var named = name.Length > 0 && Enum.TryParse(name, true, out parsed) && !char.IsDigit(name[0]);
            return named && parsed != CommandType.None ? parsed : throw new InvalidOperationException($"has a 'command' of '{name}': {advice}");
        }

        private static bool IsGameCommand(int number) => number >= (int)CommandType.GameFirst && number <= byte.MaxValue;
    }
}
