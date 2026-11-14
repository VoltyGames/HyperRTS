using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Production;
using Unity.Entities;
using UnityEngine.UIElements;
using HyperRTS.Presentation.HUD;

namespace HyperRTS.Presentation.HUD.Panels
{
    /// <summary>A producer's queue: click a slot to cancel it; the head shows its build progress and why it waits.</summary>
    public sealed class ProductionQueueView
    {
        private readonly VisualElement _root;
        private readonly VisualTreeAsset _slotTemplate;
        private int _signature;
        private readonly Label _wait = new() { name = "wait" };
        private HUDBar _headProgress;

        /// <summary>Fills <paramref name="root"/> with one <paramref name="slotTemplate"/> per queued item.</summary>
        public ProductionQueueView(VisualElement root, VisualTreeAsset slotTemplate)
        {
            _root = root;
            _slotTemplate = slotTemplate;
            _wait.AddToClassList("hud-single__queue-wait");
        }

        public void Refresh(HUDContext context, Entity producer)
        {
            var entityManager = context.EntityManager;
            var queued = entityManager.HasBuffer<ProductionQueueItem>(producer)
                ? entityManager.GetBuffer<ProductionQueueItem>(producer, true).Length
                : 0;
            var visible = queued > 0 && context.IsOwned(producer);
            _root.SetHidden(!visible);
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

            var head = entityManager.GetComponentData<Producible>(queue[0].Prefab);
            var state = entityManager.GetComponentData<Producer>(producer);
            _headProgress.Fraction = state.HeadProgress(head);
            _wait.text = HUDText.ProducerWait(state.State);
        }

        private void Rebuild(HUDContext context, Entity producer, DynamicBuffer<ProductionQueueItem> queue)
        {
            _root.Clear();
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
                _root.Add(slot);

                var progress = slot.Require<HUDBar>("progress");
                progress.SetHidden(i != 0);
                if (i == 0)
                {
                    _headProgress = progress;
                }
            }

            _root.Add(_wait);
        }
    }
}
