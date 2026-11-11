using HyperRTS.Simulation.Common.Infantry;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Areas;
using HyperRTS.Simulation.Veterancy;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Presentation.Overlays
{
    /// <summary>One chunk's optional unit components, looked up once so each unit reads them without asking the chunk.</summary>
    internal struct ChunkUnits
    {
        private readonly ArchetypeChunk _chunk;
        private ComponentTypeHandle<Retreating> _retreatingHandle;
        private readonly NativeArray<NavAgent> _agents;
        private readonly NativeArray<NavObstacle> _obstacles;
        private readonly NativeArray<Experience> _experience;
        private readonly BufferAccessor<SquadMember> _members;
        private readonly NativeArray<Suppression> _suppression;
        private readonly bool _hasMembers;
        private readonly bool _hasRetreating;

        public ChunkUnits(in ArchetypeChunk chunk, ref UnitHandles handles)
        {
            _chunk = chunk;
            _retreatingHandle = handles.Retreating;
            _agents = chunk.Has(ref handles.Agents) ? chunk.GetNativeArray(ref handles.Agents) : default;
            _obstacles = chunk.Has(ref handles.Obstacles) ? chunk.GetNativeArray(ref handles.Obstacles) : default;
            _experience = chunk.Has(ref handles.Experience) ? chunk.GetNativeArray(ref handles.Experience) : default;
            _hasMembers = chunk.Has(ref handles.Members);
            _members = _hasMembers ? chunk.GetBufferAccessor(ref handles.Members) : default;
            _suppression = chunk.Has(ref handles.Suppression) ? chunk.GetNativeArray(ref handles.Suppression) : default;
            _hasRetreating = chunk.Has(ref handles.Retreating);
        }

        public OverlayUnit Unit(int index) => new(
            _agents.IsCreated ? _agents[index] : null,
            _obstacles.IsCreated ? _obstacles[index] : null,
            _experience.IsCreated ? _experience[index].Rank : 0,
            _hasMembers ? _members[index] : default,
            _suppression.IsCreated ? _suppression[index] : null,
            _hasRetreating && _chunk.IsComponentEnabled(ref _retreatingHandle, index));
    }
}
