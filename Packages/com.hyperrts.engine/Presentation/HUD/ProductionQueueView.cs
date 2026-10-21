using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Production;
using Unity.Entities;
using UnityEngine.UIElements;

namespace HyperRTS.Presentation.HUD
{
    /// <summary>A producer's queue: click a slot to cancel it; the head shows its build progress.</summary>
    public sealed class ProductionQueueView
    {
        private readonly VisualTreeAsset _slotTemplate;
        private int _signature;
        private HUDBar _headProgress;

        /// <summary>Fills <paramref name="root"/> with one <paramref name="slotTemplate"/> per queued item.</summary>
        public ProductionQueueView(VisualElement root, VisualTreeAsset slotTemplate)
        {
            Root = root;
            _slotTemplate = slotTemplate;
        }

        public VisualElement Root { get; }

        public void Refresh(HUDContext context, Entity producer)
        {
            var entityManager = context.EntityManager;
            var queued = entityManager.HasBuffer<ProductionQueueItem>(producer)
                ? entityManager.GetBuffer<ProductionQueueItem>(producer, true).Length
                : 0;
            var visible = queued > 0 && context.IsOwned(producer);
            Root.SetHidden(!visible);
            if (!visible)
            {
                _signature = 0;
                return;
            }

            var queue = entityManager.GetBuffer<ProductionQueueItem>(producer, true);
            var signature = producer.GetHashCode() * 31 + queue.Length;
            foreach (var item in queue)
            {
                signature = signature * 31 + item.Prefab.GetHashCode();
            }

            if (signature != _signature)
            {
                _signature = signature;
                Rebuild(context, producer, queue);
            }

            _headProgress.Fraction = HeadProgress(entityManager, producer, queue[0].Prefab);
        }

        private void Rebuild(HUDContext context, Entity producer, DynamicBuffer<ProductionQueueItem> queue)
        {
            Root.Clear();
            for (var i = 0; i < queue.Length; i++)
            {
                var index = i;
                var slot = (Button)_slotTemplate.CloneRoot();
                slot.clicked += () => context.Issue(new PlayerCommand
                {
                    Type = CommandType.CancelProduction,
                    Unit = producer,
                    Argument = index,
                });
                slot.Require<HUDIcon>("icon").Bind(context.EntityManager, queue[i].Prefab);
                Root.Add(slot);

                // Only the head is in production, so the other slots drop their bar.
                var progress = slot.Require<HUDBar>("progress");
                if (i == 0)
                {
                    _headProgress = progress;
                }
                else
                {
                    progress.RemoveFromHierarchy();
                }
            }
        }

        private static float HeadProgress(EntityManager entityManager, Entity producer, Entity prefab)
        {
            var elapsed = entityManager.GetComponentData<Producer>(producer).Elapsed;
            var buildTime = entityManager.HasComponent<Producible>(prefab)
                ? entityManager.GetComponentData<Producible>(prefab).BuildTime
                : 0f;
            return buildTime > 0f ? elapsed / buildTime : 1f;
        }
    }
}
