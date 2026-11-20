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
        private static readonly string[] Actions = { "move", "attack", "attackGround", "ability", "command", "stop" };

        private readonly string _units;
        private readonly CommandType _type;
        private readonly float2? _point;
        private readonly string _target;
        private readonly int _argument;

        private OrderStep(StagingJson json)
        {
            _units = json.Text("units");
            var action = json.Choose(Actions);
            switch (action)
            {
                case "move":
                    _type = CommandType.Move;
                    _point = json.Point(action);
                    return;
                case "attackGround":
                    _type = CommandType.FireMission;
                    _point = json.Point(action);
                    return;
                case "attack":
                    _type = CommandType.Attack;
                    _target = json.Text(action);
                    return;
                case "stop":
                    _type = json.Flag(action) ? CommandType.Stop : throw json.Problem("has 'stop' false; leave the key out instead");
                    return;
            }

            // Abilities go by the hash of their name, as the command card sends them.
            var ability = action == "ability";
            _type = ability ? CommandType.UseAbility : ReadCommand(json);
            if (ability)
            {
                _argument = EntityInfo.TypeIdFromName(json.Text("ability"));
            }
            else if (json.Has("argument"))
            {
                _argument = json.Whole("argument");
            }

            _point = json.PointOrNull("at");
            _target = json.TextOrNull("target");
        }

        public override IEnumerable<string> Uses => _target == null ? new[] { _units } : new[] { _units, _target };

        public static StagingStep Read(StagingJson step) => new OrderStep(step.Child("order"));

        public override void Run(StagingRun run)
        {
            var entityManager = run.World;
            var units = run.LiveUnits(_units, "order");

            var player = run.FindPlayer(run.FindGroup(_units).Owner);
            var command = new PlayerCommand { Type = _type, Argument = _argument };
            if (_point.HasValue)
            {
                command.Position = run.GroundPoint(_point.Value);
            }

            if (_target == null)
            {
                using var listed = new NativeArray<Entity>(units.ToArray(), Allocator.Temp);
                PlayerCommands.Address(ref command, entityManager.GetBuffer<PlayerCommandSubject>(player), listed);
                entityManager.GetBuffer<PlayerCommand>(player).Add(command);
                return;
            }

            // The target lookup can change the world's structure, which drops a buffer taken before it.
            var targets = run.LiveUnits(_target, "target");
            var commands = entityManager.GetBuffer<PlayerCommand>(player);

            for (var i = 0; i < units.Count; i++)
            {
                var each = command;
                each.Unit = units[i];
                each.Target = targets[i % targets.Count];
                commands.Add(each);
            }
        }

        // A name of an engine command, or the number of a game's own.
        private static CommandType ReadCommand(StagingJson json)
        {
            var token = json.Either("command");
            var advice = $"give a CommandType name or a game command number from {(int)CommandType.GameFirst}";
            if (token.Type != JTokenType.String)
            {
                var number = (int)token;
                return IsGameCommand(number) ? (CommandType)number : throw json.Problem($"has a 'command' of {number}: {advice}");
            }

            var name = (string)token;
            var named = Enum.TryParse(name, true, out CommandType command) && !char.IsDigit(name[0]);
            return named && command != CommandType.None ? command : throw json.Problem($"has a 'command' of '{name}': {advice}");
        }

        private static bool IsGameCommand(int number) => number >= (int)CommandType.GameFirst && number <= byte.MaxValue;
    }
}
