using System.Collections.Generic;
using UnityEngine.UIElements;

namespace HyperRTS.Presentation.HUD.Panels;

/// <summary>A column of <see cref="DetailRow"/>s: rows are cloned from a template as needed and extras are hidden.</summary>
public sealed class DetailRows
{
    private readonly VisualElement _column;
    private readonly VisualTreeAsset _template;
    private readonly List<(VisualElement Row, Label Name, Label Value, VisualElement Chevrons)> _rows = new();

    public DetailRows(VisualElement column, VisualTreeAsset template)
    {
        _column = column;
        _template = template;
    }

    public void Show(List<DetailRow> rows)
    {
        _column.SetHidden(rows.Count == 0);
        while (_rows.Count < rows.Count)
        {
            var row = _template.CloneRoot();
            row.AddToClassList("hud-detail");
            _column.Add(row);
            var chevrons = new VisualElement { pickingMode = PickingMode.Ignore };
            chevrons.AddToClassList("hud-detail__chevrons");
            row.Add(chevrons);
            _rows.Add((row, row.Require<Label>("name"), row.Require<Label>("value"), chevrons));
        }

        for (var i = 0; i < _rows.Count; i++)
        {
            var shown = i < rows.Count;
            _rows[i].Row.SetHidden(!shown);
            if (shown)
            {
                SetText(_rows[i].Name, rows[i].Name);
                SetText(_rows[i].Value, rows[i].Value);
                ShowChevrons(_rows[i].Value, _rows[i].Chevrons, rows[i].Chevrons);
            }
        }
    }

    private static void ShowChevrons(Label value, VisualElement chevrons, int count)
    {
        value.SetHidden(count > 0);
        chevrons.SetHidden(count == 0);
        while (chevrons.childCount < count)
        {
            var chevron = new HUDGlyph(HUDGlyphShape.ChevronUp);
            chevron.AddToClassList("hud-detail__chevron");
            chevrons.Add(chevron);
        }

        for (var i = 0; i < chevrons.childCount; i++)
        {
            chevrons[i].SetHidden(i >= count);
        }
    }

    private static void SetText(Label label, string text)
    {
        if (label.text != text)
        {
            label.text = text;
        }
    }
}
