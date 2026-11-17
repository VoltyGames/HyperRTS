using System;
using System.Collections.Generic;
using HyperRTS.Editor.Common;
using HyperRTS.Presentation.HUD;
using Unity.Entities;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.PlayMode.Cheats
{
    /// <summary>The Strikes cheats: every strike and strategic launcher the game has, under the spawn grouping's headings, built or fired for the chosen player.</summary>
    internal class StrikesSection
    {
        private const long PollMilliseconds = 250;

        private readonly CheatsSection _cheats;
        private readonly VisualElement _rows;
        private readonly Toggle _strategic;
        private readonly List<StrikeRow> _shown = new();
        private World _world;

        /// <summary>Wires a cloned StrikesSection.uxml and polls it while the cheats are usable.</summary>
        public StrikesSection(VisualElement root, CheatsSection cheats)
        {
            _cheats = cheats;
            _rows = root.Require<VisualElement>("rows");
            _strategic = root.Require<Toggle>("strategic");
            _strategic.RegisterValueChangedCallback(change =>
                Run(chosen => MatchCheats.SetStrategicAssets(chosen.Manager, change.newValue)));
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
                    row.ShowOwned(row.Entry.Status(manager, instance), row.Entry.FindBlocker(manager, instance, point));
                }
                else
                {
                    row.ShowMissing();
                }
            }

            _strategic.SetValueWithoutNotify(MatchCheats.StrategicAssetsEnabled(manager));
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
                var strike = entry;
                row.Build.clicked += () => Run(chosen =>
                    strike.Build(chosen.Manager, chosen.Owner, SpawnCheat.ViewCenter(chosen.Manager)));
                row.Fire.clicked += () => Run(chosen => Fire(chosen, strike));
                _shown.Add(row);
                _rows.Add(row.Root);
            }
        }

        private static void Fire(ChosenPlayer chosen, StrikeEntry strike)
        {
            var owned = StrikeEntry.FindOwned(chosen.Manager, chosen.Owner);
            if (owned.TryGetValue(strike.TypeId, out var instance))
            {
                strike.Fire(chosen.Manager, chosen.Player, instance, SpawnCheat.ViewCenter(chosen.Manager));
            }
        }

        private void Run(Action<ChosenPlayer> cheat)
        {
            if (!_cheats.TryGetChosen(out var chosen))
            {
                return;
            }

            cheat(chosen);
            CheatsSection.StopRecording(chosen.Manager.World);
            Poll();
        }
    }
}
