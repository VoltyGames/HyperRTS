using System;
using System.Collections.Generic;
using HyperRTS.Editor.Common;
using HyperRTS.Presentation.HUD;
using Unity.Entities;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.PlayMode.Cheats
{
    /// <summary>The strike cheats: every strike and strategic launcher, built, readied or fired for the chosen player.</summary>
    internal class StrikesPanel
    {
        private const long PollMilliseconds = 250;

        private readonly CheatsPanel _cheats;
        private readonly VisualElement _rows;
        private readonly List<StrikeRow> _shown = new();
        private World _world;

        /// <summary>Wires a cloned StrikesPanel.uxml and polls it while the cheats are usable.</summary>
        public StrikesPanel(VisualElement root, CheatsPanel cheats)
        {
            _cheats = cheats;
            _rows = root.Require<VisualElement>("rows");
            root.schedule.Execute(Poll).Every(PollMilliseconds);
        }

        private void Poll()
        {
            if (!_cheats.TryGetChosen(out var chosen))
            {
                return;
            }

            var manager = chosen.Manager;
            if (_world != manager.World)
            {
                Rebuild(manager);
            }

            var owned = StrikeEntry.FindOwned(manager, chosen.Owner);
            var point = SpawnCheat.ViewCenter(manager);
            foreach (var row in _shown)
            {
                if (owned.TryGetValue(row.Entry.TypeId, out var instance))
                {
                    var entry = row.Entry;
                    row.ShowOwned(entry.Status(manager, instance), entry.IsReady(manager, instance),
                        entry.FindBlocker(manager, instance, point));
                }
                else
                {
                    row.ShowMissing();
                }
            }
        }

        // The strikes come from prefabs, which stay the same for the whole match.
        private void Rebuild(EntityManager manager)
        {
            _world = manager.World;
            _rows.Clear();
            _shown.Clear();
            string heading = null;
            foreach (var entry in StrikeEntry.Find(manager))
            {
                if (entry.Heading != null && entry.Heading != heading)
                {
                    heading = entry.Heading;
                    var label = new Label(heading);
                    label.AddToClassList("hrts-strike__heading");
                    _rows.Add(label);
                }

                var row = new StrikeRow(entry);
                row.Build.clicked += () => _cheats.Act((manager, _, owner) =>
                    entry.Build(manager, owner, SpawnCheat.ViewCenter(manager)));
                row.Ready.clicked += () => ActOnOwned(entry, (manager, _, instance) => entry.Ready(manager, instance));
                row.Fire.clicked += () => ActOnOwned(entry, (manager, player, instance) =>
                    entry.Fire(manager, player, instance, SpawnCheat.ViewCenter(manager)));
                _shown.Add(row);
                _rows.Add(row.Root);
            }
        }

        private void ActOnOwned(StrikeEntry strike, Action<EntityManager, Entity, Entity> cheat)
        {
            _cheats.Act((manager, player, owner) =>
            {
                if (StrikeEntry.FindOwned(manager, owner).TryGetValue(strike.TypeId, out var instance))
                {
                    cheat(manager, player, instance);
                }
            });
            Poll();
        }
    }
}
