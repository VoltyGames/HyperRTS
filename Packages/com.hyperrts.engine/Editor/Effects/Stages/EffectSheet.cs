using System.Collections.Generic;
using System.IO;
using System.Linq;
using HyperRTS.Presentation.HUD;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace HyperRTS.Editor.Effects.Stages;

/// <summary>The Effects page's images a rig's stages made, shown under its stages.</summary>
internal sealed class EffectSheet
{
    private readonly List<Texture2D> _images = [];
    private readonly VisualElement _root;

    public EffectSheet(VisualElement root)
    {
        _root = root;
    }

    /// <summary>Shows the images at the project-relative <paramref name="paths"/> that exist, or hides the sheet.</summary>
    public void Show(IEnumerable<string> paths)
    {
        _images.ForEach(Object.DestroyImmediate);
        _images.Clear();
        _root.Clear();
        foreach (var path in paths.Where(File.Exists))
        {
            var texture = new Texture2D(2, 2) { hideFlags = HideFlags.HideAndDontSave };
            texture.LoadImage(File.ReadAllBytes(path));
            _images.Add(texture);
            var image = new Image { image = texture, scaleMode = ScaleMode.ScaleToFit, tooltip = Path.GetFileName(path) };
            image.AddToClassList("hrts-effects__sheet-image");
            _root.Add(image);
        }

        _root.SetHidden(_images.Count == 0);
    }
}
