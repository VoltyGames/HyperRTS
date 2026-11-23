using HyperRTS.Simulation.Common;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Presentation.HUD.Panels
{
    /// <summary>
    /// The facts block beside a single selected entity: weapons in one column, then armor, speed, sight and
    /// passengers in the next. Read a few times a second, since none of it changes fast.
    /// </summary>
    public sealed class UnitDetails
    {
        private const float RefreshSeconds = 0.25f;

        private readonly UnitFacts _facts = new();
        private readonly DetailRows _weapons;
        private readonly DetailRows _stats;
        private float _nextRefresh;

        public UnitDetails(VisualElement root, VisualTreeAsset rowTemplate)
        {
            _weapons = new DetailRows(root.Require<VisualElement>("weapons"), rowTemplate);
            _stats = new DetailRows(root.Require<VisualElement>("facts"), rowTemplate);
        }

        /// <summary>Reads again at the next <see cref="Refresh"/>, as for a new selection.</summary>
        public void Invalidate() => _nextRefresh = 0f;

        public void Refresh(HUDContext context, Entity entity)
        {
            if (Time.unscaledTime < _nextRefresh)
            {
                return;
            }

            _nextRefresh = Time.unscaledTime + RefreshSeconds;
            var entityManager = context.EntityManager;
            var prefab = context.Registry.Find(entityManager.GetComponentData<EntityInfo>(entity).TypeId);
            _facts.Read(entityManager, entity, prefab);
            _weapons.Show(_facts.Weapons);
            _stats.Show(_facts.Facts);
        }
    }
}
