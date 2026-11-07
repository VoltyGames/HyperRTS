# Squads and cover

[Docs](../README.md) ▸ [Simulation](README.md) ▸ Squads, Suppression, Retreat, Reinforce, Cover

A squad is one unit made of several soldiers. This page covers how they take damage, move, suppress, retreat, get
replaced and use cover. Weapons are in [weapons](weapons.md) and damage in [combat](combat.md); how members are drawn is in
[squad figures](../presentation/animation.md#squad-figures).

## Squads and members

`SquadAuthoring` (module `Simulation/Squads`, data in `Simulation/Common`) makes a unit a squad while it stays one
entity for orders, movement, capture, garrisons and veterancy. Each entry of `members` names the weapon it carries (0 is
the primary, 1 and up `extraWeapons`); the unit's `maxHealth` is shared evenly among them. The baked `SquadMember`
buffer is replicated: health, `Offset`, `Posture` (`Stand`, `Crouch`, `Prone`) and `Weapon`. The squad's `Health` is
always the sum of its living members'. A dead member keeps its place in the buffer, so indices never shift.

- **Direct hits** land on one living member, picked by a roll seeded from the frame, source, target and queue place,
  so replays agree. Overkill is lost, never carried over. `SquadDamage` applies cover, hurts the member, records its
  death and adds suppression.
- **Splash** hurts every living member by its own distance from the blast, so members standing past the squad's radius
  are reached. A direct hit with a radius also splashes the members it missed.
- **Death.** A member's `SquadMember.Death` (`Shot` or `Blast`) and `BlastHeading` are replicated so figures pick
  their fall.
- **Heals** fill the most hurt living member first.
- **Weapons.** `SquadMemberSystem` sets each `WeaponMount.Carriers` to the living members carrying it. A firing makes
  one shot per carrier, and a mount with no carriers stays silent. Set a weapon's damage to the old squad damage
  divided by its carriers to keep damage per second.
- **Upgrades.** A member may name an `upgradedWeapon`, normally a research-gated extra weapon. While that mount is
  enabled (`MountUnlockSystem`) the member carries it instead of its base weapon.
- **Slots.** Members walk toward a slot: a wedge along the heading while the squad moves, a ring when it halts, never
  past `SquadRules.MaxSpread`. They close at a share of the squad's speed, so a straggler runs. Squads inside a
  container don't walk. Slots ignore the nav grid.
- **Stats.** A change to the squad's max health rescales the members.
- **Weapon teams.** A squad that is `Deployable` is a weapon team ([deploying](weapons.md#deploying)). Members with
  `takesCrewPlace` walk to their `crewOffset` while `Emplaced`, take `crewPosture` and use no cover; the rest keep
  the ring. `designation` and an upgraded weapon's `WeaponStats.designation` show on member cards.

`SquadRules` on the Match (baked from `InfantryTuning.squads`) holds the formation and pace numbers below.
`SquadMember.IsMoving`, by actual velocity, decides whether a squad is halted.

## Suppression and posture

- **Suppression.** `Suppression.Value` (0 to 1, replicated) rises with `DamageSettings.suppression` on each hit, scaled
  by the struck member's cover, and falls after a quiet delay. `SquadRules.Level` turns it into `Steady`,
  `Suppressed` or `Pinned`.
- **Pace.** `SquadPostureSystem` writes `Pace` each frame: multipliers on speed, fire rate and damage taken.
  `MovementSystem`, `WeaponFireSystem` and `DamageFactors` read it. A speed of 0 holds a pinned squad still with its
  orders kept.
- **Posture.** Only `SquadPostureSystem` sets `SquadMember.Posture`: retreating stands, pinned lies prone, a member in
  cover crouches, a moving squad stands, a halted one crouches while it has a target.

## Retreat and reinforce

- **Retreat.** `OrderType.Retreat` and `CommandType.Retreat`. `RetreatSystem` sends each commanded or selected squad
  to the nearest allied working `RetreatPoint` (`RetreatPointAuthoring`: a radius) and does nothing when there is
  none. While it runs the squad moves fast, cannot fire, takes reduced damage and loses its suppression. Any other
  order replaces it. The AI, HUD and barks read the `Retreating` enable bit.
- **Reinforce.** `CommandType.Reinforce`. `ReinforceSystem` starts `Reinforcing` on a squad with a dead member, an
  allied working `ReinforcePoint` (`ReinforcePointAuthoring`: a range) in range and resources for one member. Each
  member costs its share of the squad's `ResourceCost`, paid when its `ReinforceTime` starts and refunded if the
  reinforcing ends first. The first dead member revives at full health at the squad's centre and walks to its slot.
- **One rule set.** `SquadPoints` finds the working points an owner may use, and `SquadOrderRule` gives the
  `SquadRefusal` and the pay and refund. The systems, the AI and `SquadCommands` all call them.
- **Replays** record the squad's health and a mask of living members (up to 16), so playback shows the right soldiers
  dead.

## Cover

Cover means protection; what hides a unit from sight is concealment ([vision](combat.md#vision)).

- **Spots.** A prop carries `CoverSpot`s (`CoverSpotAuthoring`): a local position, a facing (across the prop toward the
  fire it stops) and a `CoverKind`, `Heavy` or `Light`. The inspector fillers "Fill both sides" and "Fill ring" write
  the list. `CoverSpotRule` flags spots inside the prop's nav obstacle, without a facing, facing away or far from the
  prop. A destroyed prop takes its spots with it, and a prop's spots count only once it is built.
- **Index.** `CoverSpotSystem` keeps the `CoverSpots` singleton: every spot in world space, with a `Reservation`,
  rebuilt when cover entities appear, vanish or move. A spot is named by `CoverSpotKey`, which survives a rebuild.
- **Taking a spot.** When a squad halts, `SquadCoverSystem` gives each member the best free spot within
  `CoverRules.SearchRadius`: heavy over light, facing the squad's front, nearest its ring place. No two share one.
  `SquadMember.Cover` is set once the member stands in the spot, and `CoverHeading` is the way it faces.
- **Concealing ground.** A halted member with no spot on a concealing cell (woods, tall crops) has `Light` cover from
  every side, with no arc.
- **Damage.** A hit on a member in cover from within the front arc is scaled by the kind's share, eased toward 1 by
  the damage type's `coverPiercing`. Direct hits favour exposed members. `CoverSettings` on the Match's
  `InfantryTuning` bakes `CoverRules`: damage taken, suppression taken and preference per `CoverKind`, the arc and the
  search radius.
- **Site work.** While a squad builds, `SquadWork` (enabled by `ConstructionSystem`) lines the members along the
  site's long side, `SquadMember.Action` becomes `Dig`, and the builder's rate counts once per living member.
- **Trenches.** A spot below its prop's origin gives `WorldCoverSpot.Drop`. A member in a spot deeper than
  `SquadMember.StandingDepth` stands, with the walls to its shoulders, and its figure sinks into the dent.
- **Craters.** `CraterSettings` on the Match (a prefab, `minRadius`, `maxCount`, `spacing`) bakes `CraterRules` only
  when it names a prefab. `CraterSystem` turns large ground impacts into crater props (oldest removed past the cap),
  which carry `CoverSpotAuthoring`. Craters are server-spawned, non-ghost entities; clients draw them from impact
  cues.
