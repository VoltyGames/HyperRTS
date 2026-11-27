using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Authoring;
using HyperRTS.Editor.Common;
using HyperRTS.Editor.Validation;
using HyperRTS.Presentation.HUD;
using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.GameEntities;
using HyperRTS.Simulation.Production;
using Unity.Hierarchy;
using Unity.Hierarchy.Editor;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Decorations;

/// <summary>
/// Decorates Hierarchy rows of entity objects through the Hierarchy window's UI Toolkit item events. Row data is
/// built on first bind and kept until a change to that object or one under it, or a scene or asset change, clears it, because validating on
/// every bind is too slow.
/// </summary>
internal static class HierarchyRows
{
    // The Hierarchy window and its decorated rows outlive Play mode, so this state does too.
    [NoAutoStaticsCleanup]
    private static readonly Dictionary<EntityId, HierarchyRow> Decorations = [];

    // Each bound item's view and the elements added to it, so a rebind or unbind removes them.
    [NoAutoStaticsCleanup]
    private static readonly Dictionary<HierarchyViewItem, (HierarchyView View, VisualElement Marks, VisualElement Edge)> Bound = [];

    // Rows to rebuild since the last refresh: the changed objects and their ancestors, or all of them.
    [NoAutoStaticsCleanup]
    private static readonly HashSet<EntityId> Dirty = [];

    [NoAutoStaticsCleanup]
    private static bool _dirtyAll;

    [NoAutoStaticsCleanup]
    private static bool _refreshQueued;

    [InitializeOnLoadMethod]
    private static void Install()
    {
        HierarchyWindow.BindView += (_, view) => view.StyleContainer.styleSheets.Add(EditorAssets.Sheet("Decorations"));
        HierarchyWindow.BindViewItem += (_, view, item) => Decorate(view, item);
        HierarchyWindow.UnbindViewItem += (_, _, item) => Strip(item);
        // Undo and redo publish their changes here too.
        ObjectChangeEvents.changesPublished += (ref ObjectChangeEventStream stream) =>
        {
            MarkDirty(ref stream);
            QueueRefresh();
        };
    }

    private static void MarkDirty(ref ObjectChangeEventStream stream)
    {
        for (var i = 0; i < stream.length; i++)
        {
            MarkEvent(ref stream, i);
        }
    }

    private static void MarkEvent(ref ObjectChangeEventStream stream, int i)
    {
        switch (stream.GetEventType(i))
        {
            case ObjectChangeKind.CreateGameObjectHierarchy:
                stream.GetCreateGameObjectHierarchyEvent(i, out var created);
                MarkWithAncestors(created.entityId);
                break;
            case ObjectChangeKind.ChangeGameObjectStructureHierarchy:
                stream.GetChangeGameObjectStructureHierarchyEvent(i, out var hierarchy);
                MarkWithAncestors(hierarchy.entityId);
                break;
            case ObjectChangeKind.ChangeGameObjectStructure:
                stream.GetChangeGameObjectStructureEvent(i, out var structure);
                MarkWithAncestors(structure.entityId);
                break;
            case ObjectChangeKind.ChangeGameObjectParent:
                stream.GetChangeGameObjectParentEvent(i, out var parent);
                MarkWithAncestors(parent.entityId);
                MarkWithAncestors(parent.previousParentEntityId);
                break;
            case ObjectChangeKind.ChangeChildrenOrder:
                stream.GetChangeChildrenOrderEvent(i, out var order);
                MarkWithAncestors(order.entityId);
                break;
            case ObjectChangeKind.ChangeGameObjectOrComponentProperties:
                stream.GetChangeGameObjectOrComponentPropertiesEvent(i, out var properties);
                MarkWithAncestors(properties.entityId);
                break;
            case ObjectChangeKind.DestroyGameObjectHierarchy:
                stream.GetDestroyGameObjectHierarchyEvent(i, out var destroyed);
                MarkWithAncestors(destroyed.parentEntityId);
                break;
            default:
                // Scene, asset and prefab changes name no single object, and rules read assets.
                _dirtyAll = true;
                break;
        }
    }

    private static void MarkWithAncestors(EntityId entityId)
    {
        Dirty.Add(entityId);
        var changed = EditorUtility.EntityIdToObject(entityId);
        var transform = changed switch
        {
            GameObject gameObject => gameObject.transform,
            Component component => component.transform,
            _ => null,
        };

        for (var ancestor = transform == null ? null : transform.parent; ancestor != null; ancestor = ancestor.parent)
        {
            Dirty.Add(ancestor.gameObject.GetEntityId());
        }
    }

    // One edit raises several of these events, and a drag raises them every frame, so they share one refresh.
    private static void QueueRefresh()
    {
        if (_refreshQueued)
        {
            return;
        }

        _refreshQueued = true;
        EditorApplication.delayCall += () =>
        {
            _refreshQueued = false;
            Refresh();
        };
    }

    /// <summary>Drops all row data and decorates every bound row again, as after a preference change.</summary>
    public static void RefreshAll()
    {
        _dirtyAll = true;
        Refresh();
    }

    private static void Refresh()
    {
        var all = _dirtyAll;
        if (all)
        {
            Decorations.Clear();
        }
        else
        {
            foreach (var entityId in Dirty)
            {
                Decorations.Remove(entityId);
            }
        }

        foreach (var (item, bound) in Bound.ToList())
        {
            var node = item.Node;
            if (all || Dirty.Contains(bound.View.Source.GetEntityIdFromNode(in node)))
            {
                Decorate(bound.View, item);
            }
        }

        Dirty.Clear();
        _dirtyAll = false;
    }

    private static void Decorate(HierarchyView view, HierarchyViewItem item)
    {
        Strip(item);
        if (!DecorationSettings.instance.hierarchyRowIcons)
        {
            return;
        }

        var node = item.Node;
        var row = Find(view.Source.GetEntityIdFromNode(in node));
        if (!row.Decorated)
        {
            return;
        }

        var marks = row.CreateMarks();
        item.RightCustomContainer.Add(marks);
        var edge = row.CreateOwnerEdge();
        if (edge != null)
        {
            item.RowContainer.Add(edge);
        }

        Bound[item] = (view, marks, edge);
    }

    private static void Strip(HierarchyViewItem item)
    {
        if (!Bound.Remove(item, out var bound))
        {
            return;
        }

        bound.Marks.RemoveFromHierarchy();
        bound.Edge?.RemoveFromHierarchy();
    }

    private static HierarchyRow Find(EntityId entityId)
    {
        if (!Decorations.TryGetValue(entityId, out var row))
        {
            row = Build(EditorUtility.EntityIdToObject(entityId) as GameObject);
            Decorations[entityId] = row;
        }

        return row;
    }

    private static HierarchyRow Build(GameObject gameObject)
    {
        if (gameObject == null || !gameObject.TryGetComponent<ProducibleAuthoring>(out var producible))
        {
            return default;
        }

        var icon = EditorGUIUtility.ObjectContent(producible, producible.GetType()).image;
        return new HierarchyRow(icon, OwnerColor(producible), WorstSeverity(gameObject));
    }

    private static Color OwnerColor(ProducibleAuthoring producible)
    {
        if (producible is not GameEntityAuthoring entity || entity.owner == 0)
        {
            return Color.clear;
        }

        return SceneMatch.PlayerColor(entity.owner);
    }

    private static MessageType WorstSeverity(GameObject gameObject)
    {
        var worst = MessageType.None;
        foreach (var component in gameObject.GetComponents<AuthoringBehaviour>())
        {
            var severity = ProjectValidator.Check(component).Worst;
            if (severity > worst)
            {
                worst = severity;
            }
        }

        return worst;
    }
}
