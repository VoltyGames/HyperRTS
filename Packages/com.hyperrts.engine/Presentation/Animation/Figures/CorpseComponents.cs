using HyperRTS.Simulation.Common.Infantry;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Presentation.Animation.Figures
{
    /// <summary>
    /// A fallen figure drawn by its own copies of the figure's renderers, listed after it in its
    /// <see cref="LinkedEntityGroup"/>, so it outlives its squad. A ragdoll while it has <see cref="CorpseBody"/>s; client only.
    /// </summary>
    public struct Corpse : IComponentData
    {
        public BlobAssetReference<FigureClips> Clips;

        /// <summary>World matrix of the copies, the object space the slot's skins are in.</summary>
        public float4x4 World;

        /// <summary>World velocity a blast adds to every body at the handover; zero for none.</summary>
        public float3 Throw;

        /// <summary>The live pose slot the copies draw; -1 while they play the fall clip.</summary>
        public int Slot;

        /// <summary>Clock time the fall clip may hand over to a ragdoll.</summary>
        public float HandoverAt;

        /// <summary>True once the handover time has passed, whether it became a ragdoll or keeps its fall clip.</summary>
        public bool HandedOver;

        /// <summary>Clock time the bodies freeze at the latest.</summary>
        public float SettleAt;

        /// <summary>True while every body is slower than the rest speed, since <see cref="SlowSince"/>.</summary>
        public bool Slow;

        public float SlowSince;

        public float HideAt;

        /// <summary>Metres the figure reaches from a body, padding the bodies into the copies' render bounds.</summary>
        public float Reach;
    }

    /// <summary>A ragdoll body of a <see cref="Corpse"/>, in the order of its ragdoll's bodies.</summary>
    [InternalBufferCapacity(0)]
    public struct CorpseBody : IBufferElementData
    {
        public Entity Body;

        /// <summary>From the body's world pose back to the corpse's object space at the handover.</summary>
        public float4x4 Start;
    }

    /// <summary>A joint between two bodies of a <see cref="Corpse"/>, destroyed with them when it freezes.</summary>
    [InternalBufferCapacity(0)]
    public struct CorpseJoint : IBufferElementData
    {
        public Entity Joint;
    }

    /// <summary>A bone's skinning matrix at the handover, which its body's motion since moves.</summary>
    [InternalBufferCapacity(0)]
    public struct CorpseBone : IBufferElementData
    {
        public float4x4 Handover;
    }

    /// <summary>
    /// What a squad's figures fall from once its ghost is gone: its type, owner and place, and whether its death cue
    /// came. A cleanup component, so it outlives the ghost, which a client also loses when the squad goes into fog.
    /// </summary>
    public struct SquadEnd : ICleanupComponentData
    {
        public int TypeId;
        public byte Owner;
        public float3 Position;
        public bool Died;

        /// <summary>Clock time the ghost was first seen gone; below 0 while it is there.</summary>
        public float GoneAt;
    }

    /// <summary>Per figure of a squad, what its fall needs if the ghost goes while it is on show. A cleanup buffer.</summary>
    [InternalBufferCapacity(0)]
    public struct ShownFigure : ICleanupBufferElementData
    {
        /// <summary>World matrix of the model it shows.</summary>
        public float4x4 World;

        /// <summary>Clock time its fall began, and when it hides, while it falls.</summary>
        public float FallStart;

        public float HideAt;
        public Posture Posture;
        public DeathCause Death;

        /// <summary>Unit direction on the XZ plane toward the blast that dropped it.</summary>
        public float2 BlastDirection;

        /// <summary>Alive, or falling on the squad's own figure.</summary>
        public bool Shown;

        public bool Falling;
        public bool HandedOver;
        public bool ShowsOther;

        public static ShownFigure Create(in SquadFigure figure, in SquadMember member, in float4x4 world)
        {
            var falling = figure.State == FigureState.Die;
            return new ShownFigure
            {
                World = world,
                FallStart = figure.StateSince,
                HideAt = figure.HideAt,
                Posture = member.Posture,
                Death = member.Death,
                BlastDirection = member.BlastDirection,
                Shown = member.Alive || falling,
                Falling = falling,
                HandedOver = figure.HandedOver,
                ShowsOther = figure.ShowsOther,
            };
        }
    }
}
