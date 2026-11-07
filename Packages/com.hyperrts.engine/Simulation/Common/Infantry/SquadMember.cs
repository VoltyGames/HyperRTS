using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Netcode;

namespace HyperRTS.Simulation.Common.Infantry
{
    /// <summary>How a squad member holds its body; clients pick the pose from it.</summary>
    public enum Posture : byte
    {
        Stand,
        Crouch,
        Prone,
    }

    /// <summary>What killed a squad member; figures pick their fall from it.</summary>
    public enum DeathCause : byte
    {
        None,
        Shot,
        Blast,
    }

    /// <summary>Where a squad member stands and how it holds itself while its squad's weapon is set up, such as a gunner prone behind the gun.</summary>
    public struct CrewPlace
    {
        /// <summary>False leaves the member at its slot.</summary>
        public bool Takes;

        /// <summary>Metres in the unit's frame: x to the right, y ahead.</summary>
        public float2 Offset;

        public Posture Posture;
    }

    /// <summary>
    /// One soldier of a squad, replicated on the squad's ghost like a <see cref="Combat.Weapons.WeaponPose"/>. The
    /// squad's <see cref="Health"/> is the sum of its living members; a dead one stays in the buffer so indices never
    /// shift.
    /// </summary>
    [InternalBufferCapacity(4)]
    public partial struct SquadMember : IBufferElementData
    {
        /// <summary>Splash treats a member as a disc of this radius.</summary>
        public const float Radius = 0.3f;

        [GhostField(Quantization = 10)] public float Health;
        [GhostField(Quantization = 10)] public float MaxHealth;

        /// <summary>
        /// Metres on the XZ plane from the squad's position, on world axes so a turning squad doesn't swing it. Sent in
        /// 10 cm steps, which figures smooth.
        /// </summary>
        [GhostField(Quantization = 10)] public float2 Offset;

        [GhostField] public Posture Posture;

        /// <summary>What it is doing where it stands, such as digging at a field work.</summary>
        [GhostField] public MemberAction Action;

        /// <summary>Index of the squad's <see cref="Combat.Weapons.WeaponMount"/> this member fires.</summary>
        [GhostField] public byte Weapon;

        /// <summary>
        /// The mount it fires without its upgrade; <see cref="Weapon"/> is this or <see cref="UpgradedWeapon"/>, so a
        /// client that sees them differ knows the upgraded weapon is in hand.
        /// </summary>
        [GhostField] public byte BaseWeapon;

        /// <summary>Server only: the mount it fires instead while that mount is enabled; equal to <see cref="BaseWeapon"/> when it has none.</summary>
        [GhostField(SendData = false)] public byte UpgradedWeapon;

        public readonly bool HasUpgrade => UpgradedWeapon != BaseWeapon;

        /// <summary>Server only: the offset it walks toward.</summary>
        [GhostField(SendData = false)] public float2 Slot;

        /// <summary>
        /// Protection it has now: from its spot once it stands there, or light from every side on a concealing cell
        /// (woods, tall crops) with no spot. None while it walks.
        /// </summary>
        [GhostField] public CoverKind Cover;

        /// <summary>Server only: metres below the ground it stands in its spot, such as in a trench.</summary>
        [GhostField(SendData = false)] public float Sink;

        /// <summary>World direction it faces in cover, in 256 steps round from the X axis toward Z.</summary>
        [GhostField] public byte CoverHeading;

        /// <summary>Server only: the prop of the spot it holds, or null; see <c>CoverSpot</c>.</summary>
        [GhostField(SendData = false)] public Entity SpotEntity;

        /// <summary>Server only: its index in the prop's <c>CoverSpot</c> buffer.</summary>
        [GhostField(SendData = false)] public byte SpotIndex;

        /// <summary>The designation of the weapon it carries, for its HUD card (M4A1, PKP); baked, never sent. Its upgraded weapon shows the mount's own.</summary>
        [GhostField(SendData = false)] public FixedString32Bytes Designation;

        /// <summary>What dropped it; <see cref="DeathCause.None"/> while it lives.</summary>
        [GhostField] public DeathCause Death;

        /// <summary>World direction from it toward the blast that killed it, in 256 steps round from the X axis toward Z; only for <see cref="DeathCause.Blast"/>.</summary>
        [GhostField] public byte BlastHeading;

        /// <summary>While its squad's weapon is set up it stands at <see cref="CrewOffset"/> instead of its slot, and has no cover.</summary>
        [GhostField] public bool TakesCrewPlace;

        /// <summary>Server only: metres in the unit's frame, x to the right and y ahead, it stands at while crewing.</summary>
        [GhostField(SendData = false)] public float2 CrewOffset;

        /// <summary>Server only: how it holds itself while crewing.</summary>
        [GhostField(SendData = false)] public Posture CrewPosture;

        /// <summary>A squad moving faster than this, in metres a second, walks in a wedge and holds no spot.</summary>
        private const float MovingSpeed = 0.5f;

        /// <summary>Whether a squad with this velocity is moving; every squad system decides halted by it.</summary>
        public static bool IsMoving(float2 velocity) => math.lengthsq(velocity) > MovingSpeed * MovingSpeed;

        public readonly bool Alive => Health > 0f;

        /// <summary>Whether it stands at its crew place now, given whether its squad's weapon is <paramref name="emplaced"/>.</summary>
        public readonly bool Crews(bool emplaced) => emplaced && TakesCrewPlace;

        /// <summary>Offset from the squad of its crew place when the unit faces <paramref name="heading"/>, a unit direction on the XZ plane.</summary>
        public readonly float2 CrewSlot(float2 heading) => CrewOffset.x * new float2(heading.y, -heading.x) + CrewOffset.y * heading;

        public readonly bool HasSpot => SpotEntity != Entity.Null;

        /// <summary>A spot at least this deep, in metres, is a fire trench: walls to the shoulders, so it stands.</summary>
        public const float StandingDepth = 1f;

        /// <summary>How it holds itself in cover: crouched behind a wall, standing in a trench.</summary>
        public readonly Posture CoverPosture => Sink >= StandingDepth ? Posture.Stand : Posture.Crouch;

        /// <summary>Unit direction on the XZ plane the member faces in cover.</summary>
        public readonly float2 CoverFacing => UnpackHeading(CoverHeading);

        /// <summary>Unit direction on the XZ plane from the member toward the blast that killed it.</summary>
        public readonly float2 BlastDirection => UnpackHeading(BlastHeading);

        private static float2 UnpackHeading(byte heading)
        {
            var angle = heading * (math.PI * 2f / 256f);
            return new float2(math.cos(angle), math.sin(angle));
        }

        public static byte PackHeading(float2 direction) =>
            (byte)((int)math.round(math.atan2(direction.y, direction.x) * (256f / (math.PI * 2f))) & 255);

        /// <summary>Stops holding a spot, so it walks to its slot again.</summary>
        public void LeaveSpot()
        {
            SpotEntity = Entity.Null;
            Cover = CoverKind.None;
        }

        /// <summary>World position of a member standing at <see cref="Offset"/> from a squad at <paramref name="squad"/>.</summary>
        public readonly float3 Position(float3 squad) => squad + new float3(Offset.x, 0f, Offset.y);

        /// <summary>
        /// The shares of a hit from <paramref name="from"/> this member takes: its cover's shares, or all of it with no
        /// cover or from outside the spot's front arc. <paramref name="piercing"/> is the share of protection the hit
        /// ignores.
        /// </summary>
        public readonly Exposure ExposureTo(in CoverRules rules, float3 squad, float3 from, float piercing)
        {
            var toThreat = math.normalizesafe(from.xz - Position(squad).xz);
            var covered = Cover != CoverKind.None && (!HasSpot || rules.Faces(CoverFacing, toThreat));
            return covered ? rules.Entry(Cover).Pierced(piercing) : Exposure.Open;
        }
    }
}
