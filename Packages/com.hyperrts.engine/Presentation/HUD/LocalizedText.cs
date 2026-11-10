using Unity.Localization;
using UnityEngine.UIElements;

namespace HyperRTS.Presentation.HUD
{
    /// <summary>
    /// Binds a text element to a string table entry in the selected language, filling its placeholders from
    /// <see cref="Arguments"/>; on an element with <see cref="CapsClass"/> the text shows in capitals.
    /// </summary>
    [UxmlObject]
    public partial class LocalizedText : CustomBinding
    {
        /// <summary>USS has no text case, so localized text on an element with this class shows in capitals.</summary>
        public const string CapsClass = "hrts-caps";

        public LocalizedText()
        {
            updateTrigger = BindingUpdateTrigger.WhenDirty;
        }

        public LocalizedText(string table, string entry, params object[] arguments) : this()
        {
            Table = table;
            Entry = entry;
            Arguments = arguments;
        }

        [UxmlAttribute]
        public string Table { get; set; }

        [UxmlAttribute]
        public string Entry { get; set; }

        public object[] Arguments { get; set; }

        protected override void OnActivated(in BindingActivationContext context) =>
            LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;

        protected override void OnDeactivated(in BindingActivationContext context) =>
            LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;

        protected override BindingResult Update(in BindingContext context)
        {
            var text = LocalizationSettings.ResourceDatabase.GetLocalizedString(Table, Entry, args: Arguments);
            var element = (TextElement)context.targetElement;
            element.text = element.ClassListContains(CapsClass)
                ? text.ToUpper(LocalizationSettings.SelectedLocale.CultureInfo)
                : text;
            return new BindingResult(BindingStatus.Success);
        }

        private void OnLocaleChanged(Locale locale) => MarkDirty();
    }
}
