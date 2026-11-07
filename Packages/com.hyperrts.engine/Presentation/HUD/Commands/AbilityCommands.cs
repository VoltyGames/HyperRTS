using HyperRTS.Simulation.Common;
using System.Collections.Generic;
using HyperRTS.Simulation.Abilities;
using HyperRTS.Simulation.Orders;
using Unity.Entities;
using Unity.Mathematics;
using HyperRTS.Presentation.HUD;

namespace HyperRTS.Presentation.HUD.Commands
{
    /// <summary>
    /// Command-card buttons for the selection's abilities and the local player's support powers, greyed out with a
    /// countdown while cooling down. Aimed abilities start targeting for the next world click.
    /// </summary>
    public sealed class AbilityCommands : ICommandSource
    {
        private readonly CommandKit _kit;
        private readonly List<Entry> _entries = new();
        private readonly List<Entity> _casters = new();
        private Entity _player;

        public AbilityCommands(CommandKit kit)
        {
            _kit = kit;
        }

        private sealed class Entry
        {
            public CommandButton Button;
            public int Id;
            public bool IsPower;

            /// <summary>Soonest holder's cooldown this frame; MaxValue when no holder is left.</summary>
            public float Remaining;
        }

        /// <summary>Adds a button per distinct ability of the owned casters, then per support power.</summary>
        public void Build(HUDContext context, IReadOnlyList<Entity> owned, List<CommandEntry> entries)
        {
            _entries.Clear();
            _casters.Clear();
            _player = context.ShownPlayer;

            var entityManager = context.EntityManager;
            foreach (var caster in owned)
            {
                if (!entityManager.HasBuffer<Ability>(caster))
                {
                    continue;
                }

                _casters.Add(caster);
                foreach (var ability in entityManager.GetBuffer<Ability>(caster, true))
                {
                    AddDistinct(context, entries, EntityInfo.GetTypeId(entityManager, caster), ability, false);
                }
            }

            if (entityManager.HasBuffer<Ability>(_player))
            {
                foreach (var ability in entityManager.GetBuffer<Ability>(_player, true))
                {
                    AddDistinct(context, entries, 0, ability, true);
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

        private void AddDistinct(HUDContext context, List<CommandEntry> entries, int card, in Ability ability,
            bool isPower)
        {
            if (Find(ability.Id, isPower) != null)
            {
                return;
            }

            var name = HUDText.AbilityName(ability.Name.ToString());
            var spec = _kit.Catalog.FindAbility(card, ability.Id, name);
            if (spec == null)
            {
                return;
            }

            var type = isPower ? CommandType.UsePower : CommandType.UseAbility;
            var target = ability.Target;
            var id = ability.Id;
            var entry = _kit.Create(spec, name, ability.Icon.Value, () => Use(context, type, target, id));
            entries.Add(entry);
            _entries.Add(new Entry { Button = entry.Button, Id = id, IsPower = isPower });
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
            if (!entityManager.Exists(holder) || !entityManager.HasBuffer<AbilityCooldown>(holder))
            {
                return;
            }

            foreach (var cooldown in entityManager.GetBuffer<AbilityCooldown>(holder, true))
            {
                var entry = Find(cooldown.Id, isPower);
                if (entry != null)
                {
                    entry.Remaining = math.min(entry.Remaining, math.max(0f, cooldown.Remaining));
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
            entry.Button.ShowCooldown(cooling ? (int)math.ceil(entry.Remaining) : 0);
        }
    }
}
