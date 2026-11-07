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

namespace HyperRTS.Editor.Rows
{
    /// <summary>
    /// Decorates Hierarchy rows of entity objects through the Hierarchy window's UI Toolkit item events. Row data is
    /// built on first bind and kept until the scene, an undo or any object change clears it, because validating on
    /// every bind is too slow.
    /// </summary>
    internal static class HierarchyRows
    {
        // The Hierarchy window and its decorated rows outlive Play mode, so this state does too.
        [NoAutoStaticsCleanup]
        private static readonly Dictionary<EntityId, HierarchyRow> Rows = new();

        // Each bound item's view and the elements added to it, so a rebind or unbind removes them.
        [NoAutoStaticsCleanup]
        private static readonly Dictionary<HierarchyViewItem, (HierarchyView View, VisualElement Marks, VisualElement Edge)> Bound = new();

        [NoAutoStaticsCleanup]
        private static bool _refreshQueued;

        [InitializeOnLoadMethod]
        private static void Install()
        {
            HierarchyWindow.BindView += (_, view) => view.StyleContainer.styleSheets.Add(EditorAssets.Sheet("Rows"));
            HierarchyWindow.BindViewItem += (_, view, item) => Decorate(view, item);
            HierarchyWindow.UnbindViewItem += (_, _, item) => Strip(item);
            EditorApplication.hierarchyChanged += QueueRefresh;
            Undo.undoRedoPerformed += QueueRefresh;
            ObjectChangeEvents.changesPublished += (ref ObjectChangeEventStream _) => QueueRefresh();
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

        /// <summary>Drops the row data and decorates every bound row again, as after a preference change.</summary>
        public static void Refresh()
        {
            Rows.Clear();
            foreach (var (item, bound) in Bound.ToList())
            {
                Decorate(bound.View, item);
            }
        }

        private static void Decorate(HierarchyView view, HierarchyViewItem item)
        {
            Strip(item);
            if (!EditorState.instance.hierarchyRowIcons)
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
            if (!Rows.TryGetValue(entityId, out var row))
            {
                row = Build(EditorUtility.EntityIdToObject(entityId) as GameObject);
                Rows[entityId] = row;
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
}
