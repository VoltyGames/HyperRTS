using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Cues;
using HyperRTS.Simulation.Vision;
using Unity.Entities;

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
        private FactionRelations _factions;
        private FogOfWar _fogOfWar;

        public CueVisibility(ref SystemState state)
        {
            _views = state.GetEntityQuery(ComponentType.ReadOnly<LocalFogView>());
            _relations = state.GetEntityQuery(ComponentType.ReadOnly<FactionRelations>());
            _fog = state.GetEntityQuery(ComponentType.ReadOnly<FogOfWar>());
            _view = default;
            _factions = default;
            _fogOfWar = default;
        }

        /// <summary>Reads this frame's fog view, relations and fog, waiting for the jobs that write them.</summary>
        public void Begin()
        {
            _views.CompleteDependency();
            _relations.CompleteDependency();
            _fog.CompleteDependency();
            _views.TryGetSingleton(out _view);
            _relations.TryGetSingleton(out _factions);
            _fog.TryGetSingleton(out _fogOfWar);
        }

        public readonly bool IsSeen(in CueEvent cue) => CueRules.IsSeenLocally(cue, _view, _fogOfWar, _factions);
    }
}
