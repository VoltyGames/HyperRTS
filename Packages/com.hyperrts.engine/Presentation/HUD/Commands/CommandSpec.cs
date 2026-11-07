using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Production;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HyperRTS.Presentation.HUD.Commands
{
    /// <summary>
    /// One command card button as data: what it is for, its label, icon, hotkey and where it sits. The sources decide
    /// which specs apply to the selection and show their live state; the card only places them.
    /// </summary>
    [CreateAssetMenu(menuName = HyperRTSMenu.Rig + "Command Spec", fileName = "CommandSpec")]
    public sealed class CommandSpec : ScriptableObject
    {
        [SerializeField]
        [Tooltip("What the button is for.")]
        private CommandTarget target;

        [SerializeField]
        [Tooltip("The building or unit to build, train or research (target Prefab).")]
        private ProducibleAuthoring prefab;

        [SerializeField]
        [Tooltip("The ability's name (target Ability).")]
        private string ability;

        [SerializeField]
        [Tooltip("The order to issue (target Order).")]
        private CommandType order;

        [SerializeField]
        [Tooltip("The order's argument, such as the stance (target Order).")]
        private int argument;

        [SerializeField]
        [Tooltip("The builder or producer whose card shows it; empty for every card. A spec with a card beats one without for the same target.")]
        private ProducibleAuthoring card;

        [SerializeField]
        [Tooltip("Localization key of the label; empty takes the prefab's or ability's own name.")]
        private string labelKey;

        [SerializeField]
        [Tooltip("Button picture; empty takes the prefab's or ability's own icon.")]
        private Texture2D icon;

        [SerializeField]
        [Tooltip("Key that presses the button while its page shows; None for no key.")]
        private Key hotkey;

        [SerializeField]
        [Tooltip("Page of the card, 0 for the first.")]
        [Min(0)]
        private int page;

        [SerializeField]
        [Tooltip("Grid position on the page, left to right then top to bottom, 0 to 14.")]
        [Range(0, CommandCard.Slots - 1)]
        private int slot;

        public CommandTarget Target => target;

        public ProducibleAuthoring Prefab => prefab;

        public string Ability => ability;

        public CommandType Order => order;

        public int Argument => argument;

        public ProducibleAuthoring Card => card;

        public string LabelKey => labelKey;

        public Texture2D Icon => icon;

        public Key Hotkey => hotkey;

        public int Page => page;

        /// <summary>The key as the button shows it, such as B or 1; empty for none.</summary>
        public string HotkeyText => hotkey == Key.None ? "" : hotkey.ToString().Replace("Digit", "");

        public int Slot => slot;

        /// <summary>A spec made in code, for tests and tools.</summary>
        public static CommandSpec Create(CommandTarget target, Key hotkey = Key.None, int page = 0, int slot = 0, string labelKey = "", Texture2D icon = null, CommandType order = CommandType.None,
            int argument = 0, string ability = "", ProducibleAuthoring prefab = null, ProducibleAuthoring card = null)
        {
            var spec = CreateInstance<CommandSpec>();
            spec.target = target;
            spec.hotkey = hotkey;
            spec.page = page;
            spec.slot = slot;
            spec.labelKey = labelKey;
            spec.icon = icon;
            spec.order = order;
            spec.argument = argument;
            spec.ability = ability;
            spec.prefab = prefab;
            spec.card = card;
            return spec;
        }
    }
}
