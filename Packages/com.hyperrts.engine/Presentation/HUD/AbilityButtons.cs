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
    /// countdown while cooling down. Aimed abilities start targeting for the next world click.
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

            /// <summary>Soonest holder's cooldown this frame; MaxValue when no holder is left.</summary>
            public float Remaining;

            /// <summary>Whole seconds on the countdown, so its text only changes once a second.</summary>
            public int Shown = -1;
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
                entry.Remaining = float.MaxValue;
            }

            foreach (var caster in _casters)
            {
                Consider(context.EntityManager, caster, false);
            }

            Consider(context.EntityManager, _player, true);
            foreach (var entry in _entries)
            {
                Show(entry);
            }
        }

        private void AddDistinct(HUDContext context, VisualElement root, in Ability ability, bool isPower)
        {
            if (Find(ability.Id, isPower) != null)
            {
                return;
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
                context.BeginTargeting(type, id);
            }
        }

        /// <summary>Lowers each matching entry's cooldown to this holder's, if it is sooner.</summary>
        private void Consider(EntityManager entityManager, Entity holder, bool isPower)
        {
            if (!entityManager.Exists(holder) || !entityManager.HasBuffer<Ability>(holder))
            {
                return;
            }

            foreach (var ability in entityManager.GetBuffer<Ability>(holder, true))
            {
                var entry = Find(ability.Id, isPower);
                if (entry != null)
                {
                    entry.Remaining = math.min(entry.Remaining, math.max(0f, ability.CooldownRemaining));
                }
            }
        }

        private Entry Find(int id, bool isPower)
        {
            foreach (var entry in _entries)
            {
                if (entry.Id == id && entry.IsPower == isPower)
                {
                    return entry;
                }
            }

            return null;
        }

        private static void Show(Entry entry)
        {
            var ready = entry.Remaining <= 0f;
            if (entry.Button.enabledSelf != ready)
            {
                entry.Button.SetEnabled(ready);
            }

            var cooling = !ready && entry.Remaining < float.MaxValue;
            var seconds = cooling ? (int)math.ceil(entry.Remaining) : 0;
            if (seconds == entry.Shown)
            {
                return;
            }

            entry.Shown = seconds;
            entry.Countdown.text = seconds > 0 ? seconds.ToString() : "";
        }
    }
}
