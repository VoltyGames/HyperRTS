using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Orders;
using Unity.Entities;
using UnityEngine.UIElements;

namespace HyperRTS.Presentation.Hud
{
    /// <summary>A producer's queue: click a slot to cancel it; the head shows its build progress.</summary>
    public sealed class ProductionQueueView
    {
        private int _signature;
        private VisualElement _headFill;

        public ProductionQueueView()
        {
            Root = HudElements.Box("hud-queue");
        }

        public VisualElement Root { get; }

        public void Refresh(HudContext context, Entity producer)
        {
            var entityManager = context.EntityManager;
            var visible = context.IsOwned(producer) && entityManager.HasBuffer<ProductionQueueItem>(producer)
                && entityManager.GetBuffer<ProductionQueueItem>(producer, true).Length > 0;
            HudElements.SetVisible(Root, visible);
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

            HudElements.SetFraction(_headFill, HeadProgress(entityManager, producer, queue[0].Prefab));
        }

        private void Rebuild(HudContext context, Entity producer, DynamicBuffer<ProductionQueueItem> queue)
        {
            Root.Clear();
            for (var i = 0; i < queue.Length; i++)
            {
                var index = i;
                var slot = new Button(() => context.Issue(new PlayerCommand
                {
                    Type = CommandType.CancelProduction,
                    Unit = producer,
                    Argument = index,
                }));
                slot.AddToClassList("hud-slot");
                slot.Add(HudElements.EntityIcon(context.EntityManager, queue[i].Prefab, "hud-slot__icon"));
                Root.Add(slot);

                if (i == 0)
                {
                    HudElements.Bar("hud-slot__progress", slot, out _headFill);
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
