using System.Collections.Generic;
using HyperRTS.Simulation.Abilities;
using HyperRTS.Simulation.Orders;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.UIElements;

namespace HyperRTS.Presentation.HUD
{
    /// <summary>
    /// Command-card buttons for the selection's abilities and the local player's support powers, greyed out with a
    /// countdown while cooling down. Aimed abilities arm a targeted command for the next world click.
    /// </summary>
    public sealed class AbilityButtons
    {
        private readonly List<Entry> _entries = new();
        private readonly List<Entity> _casters = new();
        private Entity _player;

        private sealed class Entry
        {
            public Button Button;
            public Label Countdown;
            public int Id;
            public bool IsPower;
        }

        /// <summary>Adds a button per distinct ability of <paramref name="casters"/>, then per support power.</summary>
        public void Build(HUDContext context, VisualElement root, List<Entity> casters)
        {
            _entries.Clear();
            _casters.Clear();
            _casters.AddRange(casters);
            _player = context.View.LocalPlayer;

            var entityManager = context.EntityManager;
            foreach (var caster in casters)
            {
                foreach (var ability in entityManager.GetBuffer<Ability>(caster, true))
                {
                    AddDistinct(context, root, ability, false);
                }
            }

            if (entityManager.HasBuffer<Ability>(_player))
            {
                foreach (var ability in entityManager.GetBuffer<Ability>(_player, true))
                {
                    AddDistinct(context, root, ability, true);
                }
            }
        }

        public void Refresh(HUDContext context)
        {
            foreach (var entry in _entries)
            {
                var remaining = Cooldown(context.EntityManager, entry);
                entry.Button.SetEnabled(remaining <= 0f);
                entry.Countdown.text = remaining > 0f && remaining < float.MaxValue ? math.ceil(remaining).ToString("0") : "";
            }
        }

        private void AddDistinct(HUDContext context, VisualElement root, in Ability ability, bool isPower)
        {
            foreach (var entry in _entries)
            {
                if (entry.Id == ability.Id && entry.IsPower == isPower)
                {
                    return;
                }
            }

            var name = ability.Name.ToString();
            var type = isPower ? CommandType.UsePower : CommandType.UseAbility;
            var target = ability.Target;
            var id = ability.Id;
            var button = new CommandButton("", () => Use(context, type, target, id));
            button.Add(new HUDIcon(ability.Icon.Value, name, "hud-command__icon"));
            HUDElements.Text(name, "hud-command__name", button);
            var countdown = HUDElements.Text("", "hud-command__cooldown", button);
            root.Add(button);
            _entries.Add(new Entry { Button = button, Countdown = countdown, Id = id, IsPower = isPower });
        }

        private static void Use(HUDContext context, CommandType type, AbilityTarget target, int id)
        {
            if (target == AbilityTarget.None)
            {
                context.Issue(new PlayerCommand { Type = type, Argument = id });
            }
            else
            {
                context.ArmCommand(type, id);
            }
        }

        /// <summary>Seconds until the soonest holder can use the ability again; MaxValue when none is left.</summary>
        private float Cooldown(EntityManager entityManager, Entry entry)
        {
            var best = float.MaxValue;
            if (entry.IsPower)
            {
                Consider(entityManager, entry.Id, _player, ref best);
                return best;
            }

            foreach (var caster in _casters)
            {
                Consider(entityManager, entry.Id, caster, ref best);
            }

            return best;
        }

        private static void Consider(EntityManager entityManager, int id, Entity holder, ref float best)
        {
            if (!entityManager.Exists(holder) || !entityManager.HasBuffer<Ability>(holder))
            {
                return;
            }

            foreach (var ability in entityManager.GetBuffer<Ability>(holder, true))
            {
                if (ability.Id == id)
                {
                    best = math.min(best, math.max(0f, ability.CooldownRemaining));
                }
            }
        }
    }
}
