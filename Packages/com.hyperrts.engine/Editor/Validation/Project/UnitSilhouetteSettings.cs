using System.Linq;
using HyperRTS.Presentation.Rendering.Silhouettes;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace HyperRTS.Editor.Validation.Project;

/// <summary>
/// The project settings unit silhouettes need: two URP Render Objects features drawing LitTeam's mask and silhouette
/// passes on the active renderers, and the <see cref="UnitRenderingLayer"/> name. Checks for the validator and their
/// quick fixes.
/// </summary>
public static class UnitSilhouetteSettings
{
    // After the opaques, skybox and URP's volumetric fog, and before the fog of war and the transparents, so smoke
    // and effects still cover the silhouettes.
    private const RenderPassEvent Event = RenderPassEvent.AfterRenderingSkybox + 2;

    /// <summary>Both passes are drawn, the mask first, so the silhouette finds the unit's visible pixels marked.</summary>
    public static bool HasFeature(UniversalRendererData data)
    {
        var mask = Index(data, UnitSilhouettes.MaskPass);
        var silhouette = Index(data, UnitSilhouettes.SilhouettePass);
        return mask >= 0 && silhouette > mask;
    }

    public static void AddFeature(UniversalRendererData data)
    {
        Add(data, "Unit Mask", UnitSilhouettes.MaskPass);
        Add(data, "Unit Silhouettes", UnitSilhouettes.SilhouettePass);
    }

    private static int Index(UniversalRendererData data, string pass) =>
        data.rendererFeatures.FindIndex(feature => feature is RenderObjects { isActive: true } objects && Draws(objects, pass));

    private static bool Draws(RenderObjects objects, string pass)
    {
        var filter = objects.settings.filterSettings;
        if (filter.PassNames == null || !filter.PassNames.Contains(pass) || filter.LayerMask == 0)
        {
            return false;
        }

        return ((uint)filter.RenderingLayerMask & UnitRenderingLayer.Mask) != 0;
    }

    private static void Add(UniversalRendererData data, string name, string pass)
    {
        var feature = ScriptableObject.CreateInstance<RenderObjects>();
        feature.settings.Event = Event;
        feature.settings.filterSettings.RenderQueueType = RenderQueueType.Opaque;
        // Render Objects starts with no layers, which draws nothing.
        feature.settings.filterSettings.LayerMask = ~0;
        feature.settings.filterSettings.PassNames = new[] { pass };
        feature.settings.filterSettings.RenderingLayerMask = UnitRenderingLayer.Mask;
        RendererFeatures.Add(data, feature, name);
    }

    /// <summary>The name of the units' rendering layer in Tags and Layers; empty when unnamed.</summary>
    public static string LayerName() => RenderingLayerNames.Name(UnitRenderingLayer.Index);

    public static void NameLayer() => RenderingLayerNames.SetName(UnitRenderingLayer.Index, UnitRenderingLayer.Name);
}
