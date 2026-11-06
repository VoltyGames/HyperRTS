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

    /// <summary>
    /// One soldier of a squad, replicated on the squad's ghost like a <see cref="Combat.Weapons.WeaponPose"/>. The squad
    /// stays one entity and its <see cref="Health"/> is the sum of its living members. A member at zero health is
    /// dead and stays in the buffer, so indices never shift.
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

        /// <summary>Server only: the mount it fires instead while that mount is enabled; only meaningful with <see cref="HasUpgrade"/>.</summary>
        [GhostField(SendData = false)] public byte UpgradedWeapon;

        [GhostField(SendData = false)] public bool HasUpgrade;

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

        /// <summary>A squad moving faster than this, in metres a second, walks in a wedge and holds no spot.</summary>
        private const float MovingSpeed = 0.5f;

        /// <summary>Whether a squad with this velocity is moving; every squad system decides halted by it.</summary>
        public static bool IsMoving(float2 velocity) => math.lengthsq(velocity) > MovingSpeed * MovingSpeed;

        public readonly bool Alive => Health > 0f;

        public readonly bool HasSpot => SpotEntity != Entity.Null;

        /// <summary>A spot at least this deep, in metres, is a fire trench: walls to the shoulders, so it stands.</summary>
        public const float StandingDepth = 1f;

        /// <summary>How it holds itself in cover: crouched behind a wall, standing in a trench.</summary>
        public readonly Posture CoverPosture => Sink >= StandingDepth ? Posture.Stand : Posture.Crouch;

        /// <summary>Unit direction on the XZ plane the member faces in cover.</summary>
        public readonly float2 CoverFacing
        {
            get
            {
                var angle = CoverHeading * (math.PI * 2f / 256f);
                return new float2(math.cos(angle), math.sin(angle));
            }
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
        /// The shares of a hit from <paramref name="from"/> this member takes, standing in a squad at
        /// <paramref name="squad"/>: its cover's shares, or all of it when it has none or the hit comes from outside the
        /// front arc of its spot (light cover on a concealing cell has no arc). <paramref name="piercing"/> is the
        /// share of the protection the hit ignores.
        /// </summary>
        public readonly Exposure ExposureTo(in CoverRules rules, float3 squad, float3 from, float piercing)
        {
            var toThreat = math.normalizesafe(from.xz - Position(squad).xz);
            var covered = Cover != CoverKind.None && (!HasSpot || rules.Faces(CoverFacing, toThreat));
            return covered ? rules.Entry(Cover).Pierced(piercing) : Exposure.Open;
        }
    }
}
