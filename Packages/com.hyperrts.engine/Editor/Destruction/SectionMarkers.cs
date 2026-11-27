using System.Collections.Generic;
using System.Linq;
using HyperRTS.Presentation.Rendering.LitTeam;
using HyperRTS.Simulation.Destruction;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Destruction;

/// <summary>
/// The team markers fixed to a sectioned building beside its model (a banner on a wall, a panel on a roof) and the
/// section each sits on, so they go when it breaks. A marker standing clear of every section is left out.
/// </summary>
internal sealed class SectionMarkers(SectionsAuthoring owner, ModelSections model)
{
    /// <summary>Metres a marker may stand off a section's box and still sit on it.</summary>
    private const float Touch = 0.1f;

    private readonly Transform _owner = owner.transform;
    private readonly string _modelPath = AssetDatabase.GetAssetPath(model);

    /// <summary>Each marker's topmost object and the index of the box in <paramref name="boxes"/> it sits on.</summary>
    public List<(GameObject Marker, int Section)> Find(List<Bounds> boxes, System.Func<Transform, Bounds> measure)
    {
        var found = new List<(GameObject, int)>();
        foreach (var marker in Roots())
        {
            var seat = Seat(measure(marker), boxes);
            if (seat >= 0)
            {
                found.Add((marker.gameObject, seat));
            }
        }

        return found;
    }

    private IEnumerable<Transform> Roots() =>
        _owner.GetComponentsInChildren<TeamMarkerAuthoring>(true)
            .Select(marker => Root(marker.transform))
            .Where(root => root != null)
            .Distinct();

    // The marker's topmost object beside the model; null under one of the model's own nodes, which it hides with.
    private Transform Root(Transform node)
    {
        while (node.parent != _owner && Source(node.parent) == null)
        {
            node = node.parent;
        }

        var above = Source(node.parent);
        return above == null || above.transform.parent == null ? node : null;
    }

    // The node of the sectioned model this object is an instance of, or null beside the model.
    private GameObject Source(Transform node)
    {
        var source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(node.gameObject);
        return source != null && AssetDatabase.GetAssetPath(source) == _modelPath ? source : null;
    }

    // The box the marker shares the most with once it reaches Touch past its own bounds; -1 when it shares none.
    private static int Seat(Bounds marker, List<Bounds> boxes)
    {
        marker.Expand(Touch * 2f);
        var seat = -1;
        var most = 0f;
        for (var i = 0; i < boxes.Count; i++)
        {
            var shared = Shared(marker, boxes[i]);
            if (shared > most)
            {
                most = shared;
                seat = i;
            }
        }

        return seat;
    }

    private static float Shared(Bounds a, Bounds b)
    {
        var size = Vector3.Min(a.max, b.max) - Vector3.Max(a.min, b.min);
        return size.x > 0f && size.y > 0f && size.z > 0f ? size.x * size.y * size.z : 0f;
    }
}
