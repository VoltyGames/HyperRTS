using HyperRTS.Simulation.Resources;
using Unity.Entities;

namespace HyperRTS.Simulation.Production;

/// <summary>A producer's <see cref="ProductionQueueItem"/> buffer with the rules over it; the head is in progress.</summary>
public struct ProductionQueue
{
    private DynamicBuffer<ProductionQueueItem> _items;

    public ProductionQueue(DynamicBuffer<ProductionQueueItem> items)
    {
        _items = items;
    }

    /// <summary>
    /// Refunds every item to whoever paid and empties the queue, for a producer that is sold, captured or
    /// destroyed. Emptied at once, so nothing finishes later this frame and nothing refunds twice.
    /// </summary>
    public void RefundAll(Stockpile stock, in BufferLookup<ResourceCost> costs)
    {
        foreach (var item in _items)
        {
            stock.Refund(item.Prefab, costs);
        }

        _items.Clear();
    }

    /// <summary>
    /// Removes the item in <paramref name="slot"/> (negative for the last) and returns its prefab; cancelling the
    /// head restarts <paramref name="producer"/>'s progress. False for a slot past the end.
    /// </summary>
    public bool TryCancel(int slot, ref Producer producer, out Entity prefab)
    {
        prefab = Entity.Null;
        var index = slot < 0 ? _items.Length - 1 : slot;
        if (index < 0 || index >= _items.Length)
        {
            return false;
        }

        prefab = _items[index].Prefab;
        _items.RemoveAt(index);
        if (index == 0)
        {
            producer.Elapsed = 0f;
        }

        return true;
    }
}
