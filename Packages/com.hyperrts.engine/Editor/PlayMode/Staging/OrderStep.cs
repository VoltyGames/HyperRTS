using System;
using System.Collections.Generic;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Orders;
using Newtonsoft.Json.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Editor.PlayMode.Staging
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

        private OrderStep(StagingObject json)
        {
            _units = json.Text("units");
            var action = json.Choose(Actions);
            _type = action switch
            {
                "move" => CommandType.Move,
                "attack" => CommandType.Attack,
                "attackGround" => CommandType.FireMission,
                "stop" => CommandType.Stop,
                "ability" => CommandType.UseAbility,
                _ => ReadCommand(json),
            };
            if (action is "move" or "attackGround")
            {
                _point = json.Point(action);
            }

            if (action == "attack")
            {
                _target = json.Text("attack");
            }

            if (action == "stop" && !json.Flag("stop"))
            {
                throw json.Problem("has 'stop' false; leave the key out instead");
            }

            if (action is "command" or "ability")
            {
                _point = json.Has("at") ? json.Point("at") : null;
                _target = json.Has("target") ? json.Text("target") : null;
                _argument = Argument(json, action);
            }
        }

        // Abilities go by the hash of their name, as the command card sends them.
        private static int Argument(StagingObject json, string action)
        {
            if (action == "ability")
            {
                return EntityInfo.TypeIdFromName(json.Text("ability"));
            }

            return json.Has("argument") ? json.Whole("argument") : 0;
        }

        public override string Kind => "order";

        public override IEnumerable<string> Uses => _target == null ? new[] { _units } : new[] { _units, _target };

        public static StagingStep Read(StagingObject step) => new OrderStep(step.Child("order"));

        public override void Run(StagingRun run)
        {
            var entityManager = run.World;
            var units = run.FindGroup(_units).Alive(entityManager);
            if (units.Count == 0)
            {
                throw new InvalidOperationException($"group '{_units}' has no live units to order");
            }

            var player = run.FindPlayer(run.FindGroup(_units).Owner);
            var command = new PlayerCommand { Type = _type, Argument = _argument };
            if (_point.HasValue)
            {
                command.Position = run.GroundPoint(_point.Value);
            }

            var commands = entityManager.GetBuffer<PlayerCommand>(player);
            if (_target == null)
            {
                using var listed = new NativeArray<Entity>(units.ToArray(), Allocator.Temp);
                PlayerCommands.Address(ref command, entityManager.GetBuffer<PlayerCommandSubject>(player), listed);
                commands.Add(command);
                return;
            }

            var targets = run.FindGroup(_target).Alive(entityManager);
            if (targets.Count == 0)
            {
                throw new InvalidOperationException($"group '{_target}' has no live units to target");
            }

            for (var i = 0; i < units.Count; i++)
            {
                var each = command;
                each.Unit = units[i];
                each.Target = targets[i % targets.Count];
                commands.Add(each);
            }
        }

        // A name of an engine command, or the number of a game's own.
        private static CommandType ReadCommand(StagingObject json)
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
