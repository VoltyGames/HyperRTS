using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Cues;
using HyperRTS.Simulation.Vision.Fog;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Presentation.Common
{
    /// <summary>
    /// Which of the frame's cues the local player gets, for the systems that play them: build it in OnCreate, call
    /// <see cref="Begin"/> once per update, then ask <see cref="IsSeen"/> per cue.
    /// </summary>
    public struct CueVisibility
    {
        private readonly EntityQuery _views;
        private readonly EntityQuery _relations;
        private readonly EntityQuery _fog;
        private LocalFogView _view;
        private OwnerRelations _owners;
        private FogOfWar _fogOfWar;

        public CueVisibility(ref SystemState state)
        {
            _views = state.GetEntityQuery(ComponentType.ReadOnly<LocalFogView>());
            _relations = state.GetEntityQuery(ComponentType.ReadOnly<OwnerRelations>());
            _fog = state.GetEntityQuery(ComponentType.ReadOnly<FogOfWar>());
            _view = default;
            _owners = default;
            _fogOfWar = default;
        }

        /// <summary>Reads this frame's fog view, relations and fog, waiting for the jobs that write them.</summary>
        public void Begin()
        {
            _views.CompleteDependency();
            _relations.CompleteDependency();
            _fog.CompleteDependency();
            _views.TryGetSingleton(out _view);
            _relations.TryGetSingleton(out _owners);
            _fog.TryGetSingleton(out _fogOfWar);
        }

        public readonly bool IsSeen(in CueEvent cue) => IsSeenLocally(cue, _view, _fogOfWar, _owners);

        /// <summary>Whether the local player sees the ground at <paramref name="position"/>.</summary>
        public readonly bool IsSeen(float3 position) => IsGroundSeenLocally(position, _view, _fogOfWar);

        /// <summary>Whether the local player sees the ground at a point: in their team's sight, or the fog view is off.</summary>
        public static bool IsGroundSeenLocally(float3 position, in LocalFogView view, in FogOfWar fog) =>
            !view.Active || !fog.IsCreated || fog.IsVisible(position, view.Team, false);

        /// <summary>
        /// Whether the local player gets the cue: their own owner-only cues, and every other cue while the fog view is
        /// inactive (no fog, or everything revealed).
        /// </summary>
        public static bool IsSeenLocally(in CueEvent cue, in LocalFogView view, in FogOfWar fog,
            in OwnerRelations relations)
        {
            if (CueRules.IsOwnerOnly(cue.Slot))
            {
                return cue.Owner == view.Viewer;
            }

            if (!view.Active)
            {
                return true;
            }

            return CueRules.IsVisible(cue, view.Viewer, fog, relations);
        }
    }
}
