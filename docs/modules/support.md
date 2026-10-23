# Support modules

[Modules](../modules.md) ▸ AI, Cues, Replays, Animation, Strategic assets

## AI

`SkirmishAISystem` plays computer players from `AISystemGroup`. Like a human, it acts only through `PlayerCommand`s. Difficulty
presets on `Match` tune it, and an `AIBuildOrder` asset sets what it builds first.

It only runs on maps with bot hints, an `AIMapHintsAuthoring` that the map builder writes. They list each slot's
resource fields, defence points and attack lanes. A map without them logs an error.

On each think, an AI player:

1. Starts the first unmet build-order step it can afford.
2. Finishes abandoned sites and sends idle harvesters to work.
3. Trains units and researches.
4. Uses ready abilities and powers.
5. Once enough combat units are idle, attack-moves them along a lane to the nearest hostile building.

The lane is chosen from the match seed, so replays repeat it. To replace the AI, disable the system and write
your own `PlayerCommand`s.

## Audio

Part of `Cues`. `EntitySoundsAuthoring` gives an entity a `SoundCue` asset per cue slot. A `SoundCue` holds clips,
volume and pitch ranges, priority and 3D settings. Games add their own slots from `GameFirst` up. A cue's
`Variant` byte picks among one slot's entries: the weapon for Fire, the kind for Announce. On the wire it shares a
byte with an impact's material, which only impacts use.

| Slot | Plays | Heard by |
| --- | --- | --- |
| Fire, Impact, Death, Ability | Each shot, where it lands, on death, on an ability at the aim point | Whoever sees it |
| ProductionDone | At the producer when a unit or research finishes | Owner |
| Ready, Select, Move, Attack | Voice when produced or built, selected, ordered | Owner |
| AbilityVoice | Voice at the caster when it uses an ability | Owner |
| UnderFire | Voice when hit after 10 s unhurt, at most once per 5 s per player | Owner |

Sounds play through [cues](#cues). Playback is on the [client](client.md#audio), and the server plays nothing.

### Loops

`EntityLoopsAuthoring` gives an entity continuous sounds driven by replicated state, attached to it in 3D:

- `engineIdle` and `engineMove` crossfade by speed up to `fullSpeed`, and the pitch rises by `pitchRise`.
- `rotor` follows the [motion rig](client.md#motion-rig)'s `RotorSpin`, so it stays in step with the blades even
  while the entity is hidden. Without a rig rotor it plays full while a `Flight` entity is not `Docked`.
- `turretTraverse` plays while any weapon turns.
- `working` plays while a building is under construction or a producer has a queue.
- `ambient` always plays.

Loops fall silent when the entity dies, hides in fog or leaves the cue's range. Only the nearest `maxInstances` of a
cue play.

### Announcer

The simulation writes owner-only `Announce` cues whose `AnnouncementKind` is unit lost, base under attack, low
power, construction complete, unit ready or upgrade complete. A game numbers its own kinds from
`AnnouncementKind.GameFirst` (128) up and writes them with `CueWriter.Announce`. `AnnouncementGate` lets each
player hear at most one of a kind per second, low power included. An `AnnouncerSet` asset lists a line per kind: a
2D `SoundCue` with its own cooldown. A kind without a line stays silent. Base under attack defaults to 30 s.

`AnnouncerAuthoring` in the match's subscene picks a set per `PlayerSide`. A game may rewrite the `AnnouncerVoice`
buffer on a client at runtime, for example for the chosen language. The client queues alerts and speaks them one at
a time. The lowest cue priority goes first, and lines that wait longer than `maxWait` are dropped.

## Effects

Part of `Cues`. Particle rigs that play on cue slots, much like sounds.

| Authoring | Plays |
| --- | --- |
| `EntityEffectsAuthoring` | A rig per cue slot. Fire plays at a `muzzle` socket |
| `ImpactEffectsAuthoring` | A rig per surface material where a weapon's shots land |
| `HitMaterialAuthoring` | Sets what an entity is made of, for hits on it |
| `LoopEffectsAuthoring` | Rigs that stream while shown, such as exhaust or smoke |
| `EffectTrailAuthoring` | A rig along a projectile's path |
| `MotionDustAuthoring` | Dust by ground material and speed |
| `RotorWashAuthoring` | Dust under a low rotorcraft by ground material, thicker lower down |

Rigs play through [cues](#cues), on the client.

## Cues

A cue is a one-off event to hear or see: a shot, an impact, a death. Simulation systems write cues with `CueWriter`
into one queue. Sounds, effects and impact marks all read it, and the network and replays carry it.

- A cue is only queued if its source has a sound or effect for the slot.
- Voices, production done and announcements reach their owner. Other cues reach whoever can see the source.
  `CueRules` holds this rule.
- Games append cues before the lifecycle phase ends. The queue is emptied each frame.
- A unit says a voice line at most once a second.

## Replays

Replays record what happened, not the commands, because the simulation is not deterministic.

- `ReplayRecorder` samples every game entity at 10 Hz, plus every cue except owner-only ones.
- `ReplaySerializer` saves and loads the file. Other format versions are rejected.
- `ReplayViewer` plays a replay in a local world with the fog revealed. It can seek.

## Animation

Animates machines such as pumpjacks, wind turbines, radar antennas and cranes. Put `MachineAuthoring` on the root and
`MachinePartAuthoring` on each moving child. A part can `Spin`, `Swing` in step with the spin, `Slide` to and fro
along its axis (a crane on its rail, amplitude in metres), act as a `Link` rod between two transforms, or turn
`Downwind` about its axis so its forward, plus `offset` degrees, follows the scene's `Wind` (a windsock).

A machine's `runs` says when its clock turns: `Always`, `Powered` while its building is complete and not `Offline` (a
radar stops on low power), `Producing` while its building's queue has work that isn't halted (a crane runs only during
production), or `Firing` for a second after any of its building's weapons or interceptor fired (a gatling's barrels),
read from the replicated `WeaponPose` shot counts. The clock eases to a stop and back over `rampSeconds` rather than
snapping. A machine first seen mid-match starts at the speed it should have.

`ProductionDoorAuthoring` on a child of a producer opens that door for each unit the producer releases: it moves by
`openOffset`, turns `openAngle` about `openAxis` and scales by `openScale`, eased over `moveSeconds`, holds for
`holdSeconds`, then closes. A roll-up curtain pivoted at the top of its opening sets `openScale` to (1, 0.05, 1) and
coils up toward the pivot; a scaling door bakes a `PostTransformMatrix`.
`ProductionDoorSystem` reads `Producer.Released`, a replicated count of released units, so every client sees it,
enemies included. The owner-only production cue can't drive it. A building first seen mid-match adopts the count
without opening, and a release while the door closes sends it back up from where it stands.
Under an aircraft with `FlightAuthoring` instead, such as a cargo ramp or bomb bay, the door stays open while the
aircraft's replicated `DropRun.Active` is set over an airdrop or strike, then holds and closes. Set `holdSeconds` to
0 there, since the run already lasts past the drop.

## Strategic assets

Part of `Airdrops`. `StrategicAssetAuthoring` on a building turns one of its `AbilityAuthoring` entries, named by
`ability`, into a strategic asset. That entry targets a point, has range 0 and starts on cooldown: its cooldown is
the charge, about 300 s. The owner fires it from the command card like any building ability.

- `AbilitySystem` pauses the charge while the building is offline. `StrategicAssetSystem` starts it over for a new
  owner and holds it while `MatchRules.StrategicAssets` is off. `MatchSetup.StrategicAssets` sets that rule: on by
  default, and the game turns it off for ranked 1v1.
- Each player's `StrategicCharge` (has an asset, seconds left, duration) replicates on the player ghost, so every
  client shows the map-wide countdown. Losing the building clears it.
- Firing sends the `aircraft` in from the owner's map edge on a `DropRoute`. It carries `StealthAuthoring` and loses
  its stealth `revealSeconds` before the drop, so fighters and SAMs can engage it. Shot down first, the strike is
  lost.
- Over the target it drops a `BombLoad` from its release point, as an [airdrop](economy.md#airdrops) does, with its
  `DropRun` set either side: `count` guided bombs, homing projectiles from the `bomb` prefab that seek
  enemy structures within `zoneRadius`, nearest first, and fall in a line along the run when there are none. Each
  enemy weapon or mount that can hit air, is not offline and reaches the target removes one bomb, down to
  `maxReduction` (half) of the stick.
