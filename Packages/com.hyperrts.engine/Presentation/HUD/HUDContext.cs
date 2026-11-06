using HyperRTS.Simulation.Common.Infantry;
using HyperRTS.Simulation.Common.Prefabs;
using System.Collections.Generic;
using HyperRTS.Presentation.Common;
using HyperRTS.Presentation.Interaction;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Production;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Selection;
using HyperRTS.Simulation.Vision;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Presentation.HUD
{
    /// <summary>What every HUD widget reads (match, selection) and the only way they write back to ECS.</summary>
    public sealed class HUDContext
    {
        private readonly LiveQuery _selected = new(entityManager => entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<Selected>(),
            ComponentType.ReadOnly<EntityInfo>(),
            ComponentType.Exclude<FogHidden>()));

        private readonly LiveQuery _completed = new(entityManager =>
            CompletedBuildings.Query(Allocator.Temp).Build(entityManager));

        private readonly LiveSingleton<PointerState> _pointer = new();
        private readonly LiveSingleton<CameraFocusRequest> _cameraFocus = new();
        private readonly LiveSingleton<HotkeyPresses> _hotkeys = new();
        private readonly LiveSingleton<PrefabRegistry> _registry = new();
        private readonly LiveSingleton<SquadRules> _squadRules = new();

        private readonly LiveQuery _queues = new(entityManager =>
            ResearchRules.QueueQuery(Allocator.Temp).Build(entityManager));

        private readonly LiveQuery _limited = new(entityManager =>
            BuildCounts.Query(Allocator.Temp).Build(entityManager));

        /// <summary>How often purchase buttons re-read stock, queues and prerequisites.</summary>
        private const float PurchaseInterval = 0.1f;

        private Entity _picked;
        private float _nextPurchase;

        public MatchView View { get; private set; }

        /// <summary>Selected, visible entities this frame.</summary>
        public List<Entity> Selected { get; } = new();

        /// <summary>
        /// Changes whenever the selected set, an owner or a construction state in it changes, so widgets know when to
        /// rebuild (a captured or finished building gets a new card).
        /// </summary>
        public int SelectionHash { get; private set; }

        /// <summary>True on the frames purchase buttons re-read the match: at a fixed rate and on a new selection.</summary>
        public bool PurchasesDue { get; private set; }

        /// <summary>The player the HUD shows: the one picked with <see cref="ShowPlayer"/>, else the local player.</summary>
        public Entity ShownPlayer { get; private set; }

        public bool HasShownPlayer => ShownPlayer != Entity.Null;

        public byte ShownFaction { get; private set; }

        /// <summary>False when the shown player is not the local one: the card then shows but its buttons are disabled.</summary>
        public bool CanCommand => View.HasLocalPlayer && ShownPlayer == View.LocalPlayer;

        public EntityManager EntityManager => View.EntityManager;

        /// <summary>Shows <paramref name="player"/>'s side, for an observer or a replay; <c>Entity.Null</c> goes back to the local player.</summary>
        public void ShowPlayer(Entity player) => _picked = player;

        /// <summary>Reads this frame's selection through <paramref name="view"/>; false until it is ready.</summary>
        public bool Refresh(MatchView view)
        {
            View = view;
            if (!view.IsReady)
            {
                Selected.Clear();
                SelectionHash = 0;
                ShownPlayer = Entity.Null;
                PurchasesDue = true;
                return false;
            }

            FindShownPlayer();
            var previous = SelectionHash;
            ReadSelection();
            var timeUp = UnityEngine.Time.unscaledTime >= _nextPurchase;
            PurchasesDue = timeUp || SelectionHash != previous;
            if (timeUp)
            {
                _nextPurchase = UnityEngine.Time.unscaledTime + PurchaseInterval;
            }

            return true;
        }

        private void FindShownPlayer()
        {
            var picked = _picked != Entity.Null && EntityManager.Exists(_picked);
            ShownPlayer = picked ? _picked : View.LocalPlayer;
            ShownFaction = HasShownPlayer ? EntityManager.GetComponentData<Player>(ShownPlayer).Faction : default;
        }

        private void ReadSelection()
        {
            Selected.Clear();
            using var entities = _selected.In(EntityManager).ToEntityArray(Allocator.Temp);
            var hash = entities.Length;
            foreach (var entity in entities)
            {
                Selected.Add(entity);
                hash += StateHash(entity);
            }

            SelectionHash = hash;
        }

        public bool IsOwned(Entity entity) =>
            EntityManager.HasComponent<Faction>(entity)
            && EntityManager.GetComponentData<Faction>(entity).Value == ShownFaction;

        public DynamicBuffer<ResourceStock> Stock => EntityManager.GetBuffer<ResourceStock>(ShownPlayer, true);

        /// <summary>The shown player as a buyer this frame, shared by every button that checks a purchase.</summary>
        public Buyer SnapshotBuyer() =>
            Buyer.Snapshot(EntityManager, _queues.In(EntityManager), _limited.In(EntityManager), ShownFaction,
                new CompletedBuildings(_completed.In(EntityManager), Allocator.Temp),
                EntityManager.GetBuffer<CompletedResearch>(ShownPlayer, true));

        /// <summary>The match's squad rules; false before the match is baked.</summary>
        public bool TryGetSquadRules(out SquadRules rules) => _squadRules.TryGet(EntityManager, out rules);

        /// <summary>The letters and digits pressed last frame while gameplay input was live; none without the singleton.</summary>
        public HotkeyPresses Hotkeys => _hotkeys.TryGet(EntityManager, out var presses) ? presses : default;

        public PlacementState Placement => View.Placement;

        public int CommandPage => View.CommandPage;

        public void ShowCommandPage(int page) => View.SetCommandPage(page);

        public PurchaseRefusal FindRefusal(Entity prefab, in Buyer buyer) =>
            PurchaseRules.FindRefusal(EntityManager, prefab, buyer, Stock.AsNativeArray());

        /// <summary>Adds the prefabs of <paramref name="prefab"/>'s unmet prerequisites to <paramref name="missing"/>.</summary>
        public void FindMissingPrerequisites(Entity prefab, in Buyer buyer, List<Entity> missing)
        {
            var registry = _registry.Get(EntityManager);
            foreach (var prerequisite in EntityManager.GetBuffer<Prerequisite>(prefab, true))
            {
                var required = registry.Find(prerequisite.TypeId);
                var unmet = !buyer.Buildings.IsMet(buyer.Faction, prerequisite.TypeId, buyer.Research);
                if (unmet && required != Entity.Null)
                {
                    missing.Add(required);
                }
            }
        }

        /// <summary>Share of <paramref name="prefab"/>, at the head of <paramref name="producer"/>'s queue, built.</summary>
        public float HeadProgress(Entity producer, Entity prefab)
        {
            var elapsed = EntityManager.GetComponentData<Producer>(producer).Elapsed;
            var buildTime = EntityManager.GetComponentData<Producible>(prefab).BuildTime;
            return buildTime > 0f ? math.saturate(elapsed / buildTime) : 1f;
        }

        /// <summary>True while <paramref name="entity"/> is offline because its owner's power is low.</summary>
        public bool IsUnpowered(Entity entity) =>
            EntityManager.HasEnabled<Offline>(entity)
            && (EntityManager.GetComponentData<Offline>(entity).Reasons & OfflineReason.Unpowered) != 0;

        /// <summary>Starts targeting a command; the input layer issues it on the next world click.</summary>
        public void BeginTargeting(CommandType type, int argument) =>
            View.SetPending(new PendingCommand { Type = type, Argument = argument });

        public void Issue(PlayerCommand command)
        {
            if (CanCommand)
            {
                EntityManager.GetBuffer<PlayerCommand>(ShownPlayer).Add(command);
            }
        }

        /// <summary>Hands the building to the input layer, which moves the ghost and confirms placement.</summary>
        public void StartPlacement(Entity prefab) =>
            View.SetPlacement(new PlacementState { Active = true, Prefab = prefab });

        /// <summary>Asks the camera to centre on a ground point.</summary>
        public void FocusCamera(float3 point) =>
            View.Write(_cameraFocus, new CameraFocusRequest { Pending = true, Point = point });

        public void SetPointerOverUI(bool over)
        {
            if (!View.IsLive)
            {
                return;
            }

            var changed = _pointer.TryGet(EntityManager, out var pointer) && pointer.OverUI != over;
            if (changed)
            {
                _pointer.Set(EntityManager, new PointerState { OverUI = over });
            }
        }

        private int StateHash(Entity entity)
        {
            var faction = 0;
            if (EntityManager.HasComponent<Faction>(entity))
            {
                faction = EntityManager.GetComponentData<Faction>(entity).Value;
            }

            var building = EntityManager.HasEnabled<ConstructionProgress>(entity) ? 1 : 0;
            return (int)math.hash(new int4(entity.Index, entity.Version, faction, building));
        }
    }
}
