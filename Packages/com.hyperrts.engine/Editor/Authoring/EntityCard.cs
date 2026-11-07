using HyperRTS.Editor.Common;
using HyperRTS.Presentation.HUD;
using HyperRTS.Simulation.GameEntities;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Authoring
{
    /// <summary>A unit or building's icon, name, role, owner colour and stats, shown at the top of its inspector.</summary>
    [UxmlElement]
    public sealed partial class EntityCard : VisualElement
    {
        private readonly Image _icon;
        private readonly Label _name;
        private readonly Label _role;
        private readonly VisualElement _owner;
        private readonly VisualElement _stats;

        public EntityCard()
        {
            EditorAssets.Layout(typeof(EntityCard)).CloneTree(this);
            _icon = this.Require<Image>("icon");
            _name = this.Require<Label>("name");
            _role = this.Require<Label>("role");
            _owner = this.Require<VisualElement>("owner");
            _stats = this.Require<VisualElement>("stats");
        }

        public void Show(GameEntityAuthoring entity)
        {
            _icon.image = EntitySummary.ProducibleIcon(entity);
            _icon.SetHidden(entity.icon == null);
            _name.text = entity.DisplayName;
            _role.text = EntitySummary.Role(entity);
            _owner.style.backgroundColor = SceneMatch.PlayerColor(entity.owner);
            _owner.tooltip = entity.owner == 0 ? "Neutral" : $"Player {entity.owner}";

            _stats.Clear();
            foreach (var (label, value) in EntitySummary.Stats(entity))
            {
                _stats.Add(CreateStat(label, value));
            }
        }

        private static VisualElement CreateStat(string label, string value)
        {
            var stat = new VisualElement();
            stat.AddToClassList("hrts-card__stat");
            var name = new Label(label);
            name.AddToClassList("hrts-card__stat-name");
            var number = new Label(value);
            number.AddToClassList("hrts-card__stat-value");
            stat.Add(name);
            stat.Add(number);
            return stat;
        }
    }
}
