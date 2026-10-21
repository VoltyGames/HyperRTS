using System.Collections.Generic;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Resources;
using Unity.Entities;
using UnityEngine.UIElements;

namespace HyperRTS.Presentation.HUD
{
    /// <summary>Top bar: the local player's stockpile per resource type, population used / cap and power.</summary>
    public sealed class ResourceBar : HUDPanel
    {
        private const string FullPopulationClass = "hud-resource__amount--full";
        private const string LowPowerClass = "hud-resource__amount--low-power";

        private readonly VisualTreeAsset _entryTemplate;
        private readonly VisualElement _resources;
        private readonly Label _population;
        private readonly VisualElement _powerEntry;
        private readonly Label _power;
        private readonly List<Label> _amounts = new();
        private readonly List<int> _shown = new();
        private int _typesHash = -1;
        private Population _shownPopulation = new() { Used = -1 };
        private PowerGrid _shownPower = new() { Produced = -1f };

        public ResourceBar(HUDPanelLayouts layouts) : base(layouts.ResourceBar)
        {
            _entryTemplate = layouts.ResourceBarEntry;
            _resources = Root.Require<VisualElement>("resources");
            Root.Require<Label>("population-name").text = HUDText.Text("hud.pop");
            _population = Root.Require<Label>("population");
            _powerEntry = Root.Require<VisualElement>("power-entry");
            Root.Require<Label>("power-name").text = HUDText.Text("hud.power");
            _power = Root.Require<Label>("power");
        }

        public override void Refresh(HUDContext context)
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
            RefreshPower(context);
        }

        private void Rebuild(DynamicBuffer<ResourceStock> stock)
        {
            _resources.Clear();
            _amounts.Clear();
            _shown.Clear();
            foreach (var item in stock)
            {
                var type = item.Type.Value;
                var name = HUDText.ResourceName(type);
                var hasIcon = type.icon != null;
                var entry = _entryTemplate.CloneRoot();
                _resources.Add(entry);

                var icon = entry.Require<HUDIcon>("icon");
                icon.SetHidden(!hasIcon);
                if (hasIcon)
                {
                    icon.Bind(type.icon, name);
                }

                var label = entry.Require<Label>("name");
                label.text = name.ToUpperInvariant();
                label.SetHidden(hasIcon);

                var amount = entry.Require<Label>("amount");
                amount.style.color = type.color;
                _amounts.Add(amount);
                _shown.Add(int.MinValue);
            }
        }

        /// <summary>Shown once the player has any power plant or consumer; red while consumption exceeds supply.</summary>
        private void RefreshPower(HUDContext context)
        {
            var player = context.View.LocalPlayer;
            var grid = context.EntityManager.HasComponent<PowerGrid>(player)
                ? context.EntityManager.GetComponentData<PowerGrid>(player)
                : default;
            if (grid.Produced == _shownPower.Produced && grid.Consumed == _shownPower.Consumed)
            {
                return;
            }

            _shownPower = grid;
            var hasGrid = grid.Produced > 0f || grid.Consumed > 0f;
            _powerEntry.SetHidden(!hasGrid);
            _power.text = $"{grid.Consumed:0} / {grid.Produced:0}";
            _power.EnableInClassList(LowPowerClass, grid.IsLow);
        }

        private void RefreshPopulation(HUDContext context)
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
            _population.EnableInClassList(FullPopulationClass, population.Used >= population.Cap);
        }
    }
}
