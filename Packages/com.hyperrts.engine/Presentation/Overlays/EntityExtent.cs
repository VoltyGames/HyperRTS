using System;
using System.Collections.Generic;
using HyperRTS.Presentation.Rendering.Parts;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

namespace HyperRTS.Presentation.Overlays;

/// <summary>
/// Approximate rendered height of a unit or building, for placing overlays above it. Remembers which parts of each
/// root carry bounds, so a frame reads only their heights.
/// </summary>
public sealed class EntityExtent : IDisposable
{
    private sealed class Entry
    {
        public readonly List<Entity> Parts = new();
        public int GroupLength;
        public int Frame;
    }

    private readonly Dictionary<Entity, Entry> _entries = new();
    private readonly List<Entity> _unseen = new();
    private NativeList<Entity> _scratch = new(Allocator.Persistent);
    private int _frame;

    /// <summary>
    /// Top of the highest rendered part (models are often children of a mesh-less root), or a guess from the
    /// radius when nothing has bounds yet.
    /// </summary>
    public float Top(EntityManager entityManager, Entity entity, float3 position, float radius)
    {
        if (!_entries.TryGetValue(entity, out var entry))
        {
            entry = new Entry();
            _entries.Add(entity, entry);
            Rebuild(entityManager, entity, entry);
        }

        entry.Frame = _frame;
        var top = PartsTop(entityManager, entry);
        if (top == float.MinValue || GroupLength(entityManager, entity) != entry.GroupLength)
        {
            Rebuild(entityManager, entity, entry);
            top = PartsTop(entityManager, entry);
        }

        return top > float.MinValue ? top : position.y + radius * 2f;
    }

    /// <summary>Drops the roots no <see cref="Top"/> call asked about since the last call, so dead ones don't pile up.</summary>
    public void EndFrame()
    {
        foreach (var pair in _entries)
        {
            if (pair.Value.Frame != _frame)
            {
                _unseen.Add(pair.Key);
            }
        }

        foreach (var root in _unseen)
        {
            _entries.Remove(root);
        }

        _unseen.Clear();
        _frame++;
    }

    public void Dispose() => _scratch.Dispose();

    private static int GroupLength(EntityManager entityManager, Entity root) =>
        entityManager.HasBuffer<LinkedEntityGroup>(root) ? entityManager.GetBuffer<LinkedEntityGroup>(root, true).Length : -1;

    // Float.MinValue when no part is left with bounds, which is also when the cache is stale.
    private static float PartsTop(EntityManager entityManager, Entry entry)
    {
        var top = float.MinValue;
        foreach (var part in entry.Parts)
        {
            if (!entityManager.Exists(part) || !entityManager.HasComponent<WorldRenderBounds>(part))
            {
                return float.MinValue;
            }

            top = math.max(top, entityManager.GetComponentData<WorldRenderBounds>(part).Value.Max.y);
        }

        return top;
    }

    private void Rebuild(EntityManager entityManager, Entity root, Entry entry)
    {
        entry.GroupLength = GroupLength(entityManager, root);
        entry.Parts.Clear();
        RenderHierarchy.Collect(entityManager, root, _scratch);
        foreach (var part in _scratch)
        {
            if (entityManager.HasComponent<WorldRenderBounds>(part))
            {
                entry.Parts.Add(part);
            }
        }
    }
}
