using System.Collections.Generic;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Resources;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Presentation.Hud
{
    /// <summary>Top bar: the local player's stockpile per resource type and population used / cap.</summary>
    public sealed class ResourceBar
    {
        private static readonly Color FullPopulation = new(1f, 0.4f, 0.35f);

        private readonly VisualElement _resources;
        private readonly Label _population;
        private readonly List<Label> _amounts = new();
        private readonly List<int> _shown = new();
        private int _typesHash = -1;
        private Population _shownPopulation = new() { Used = -1 };

        public ResourceBar()
        {
            Root = HudElements.Box("hud-topbar");
            Root.AddToClassList("hud-panel");
            _resources = HudElements.Box("hud-topbar__resources", Root);

            var population = HudElements.Box("hud-resource", Root);
            HudElements.Text("POP", "hud-resource__name", population);
            _population = HudElements.Text("", "hud-resource__amount", population);
        }

        public VisualElement Root { get; }

        public void Refresh(HudContext context)
        {
            var stock = context.Stock;
            var hash = stock.Length;
            foreach (var item in stock)
            {
                hash = hash * 31 + item.Type.GetHashCode();
            }

            if (hash != _typesHash)
            {
                Rebuild(stock);
                _typesHash = hash;
            }

            for (var i = 0; i < stock.Length; i++)
            {
                if (_shown[i] != stock[i].Amount)
                {
                    _shown[i] = stock[i].Amount;
                    _amounts[i].text = stock[i].Amount.ToString("N0");
                }
            }

            RefreshPopulation(context);
        }

        private void Rebuild(DynamicBuffer<ResourceStock> stock)
        {
            _resources.Clear();
            _amounts.Clear();
            _shown.Clear();
            foreach (var item in stock)
            {
                var type = item.Type.Value;
                var name = type != null ? type.displayName : "?";
                var entry = HudElements.Box("hud-resource", _resources);
                if (type != null && type.icon != null)
                {
                    entry.Add(HudElements.Icon(type.icon, name, "hud-resource__icon"));
                }
                else
                {
                    HudElements.Text(name.ToUpperInvariant(), "hud-resource__name", entry);
                }

                var amount = HudElements.Text("", "hud-resource__amount", entry);
                amount.style.color = type != null ? type.color : Color.white;
                _amounts.Add(amount);
                _shown.Add(int.MinValue);
            }
        }

        private void RefreshPopulation(HudContext context)
        {
            var player = context.View.LocalPlayer;
            if (!context.EntityManager.HasComponent<Population>(player))
            {
                return;
            }

            var population = context.EntityManager.GetComponentData<Population>(player);
            if (population.Used == _shownPopulation.Used && population.Cap == _shownPopulation.Cap)
            {
                return;
            }

            _shownPopulation = population;
            _population.text = $"{population.Used} / {population.Cap}";
            _population.style.color = population.Used >= population.Cap
                ? new StyleColor(FullPopulation)
                : new StyleColor(StyleKeyword.Null);
        }
    }
}
