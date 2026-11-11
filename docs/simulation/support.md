# Support modules

[Docs](../README.md) ▸ [Simulation](README.md) ▸ AI, Cues, Replays, Strategic assets

Computer players, sound and effect cues, replays and map-wide strategic strikes.

## AI

`SkirmishAISystem` plays computer players from `AISystemGroup`. Like a human, it acts only through `PlayerCommand`s.
Difficulty presets on `Match` tune it through `AITuning`, and an `AIBuildOrder` asset sets what it builds first.

It only runs on maps with bot hints, an `AIMapHintsAuthoring` the map builder writes. They list each slot's resource
fields, defence points and attack lanes. A map without them logs an error.

`SkirmishAISystem` only chooses which players think this frame, takes an `AISnapshot` of the shared query results and
runs one `AITurn` per player through the planners in a fixed order: build order, builders (resuming unfinished
sites), squads, harvesters, then training and field works once the build order is done, deploy, attack and abilities
for players with `UseAbilities`. Each planner (`BuildOrderPlanner`, `BuilderPlanner`, `EconomyPlanner`,
`SquadPlanner`, `FieldWorkPlanner`, `DeployPlanner`, `AttackPlanner`, `AbilityPlanner`) is a struct in its own file
with the queries only it uses. They all share the turn's busy units and `Buyer` budget, so training never spends
what the build order already committed. A planner that calls another holds it, as the build order holds the builders.

On each think, an AI player:

1. Starts the first unmet build-order step it can afford.
2. Finishes abandoned sites and sends idle harvesters to work.
3. Trains units and researches.
4. Uses ready abilities and powers.
5. Looks after its squads: a pinned or badly hurt squad (`retreatLivingShare`, `retreatHealthShare`) gets
   `CommandType.Retreat`, and an idle squad with dead members near a friendly reinforce point gets
   `CommandType.Reinforce`.
6. Once the build order is done, digs in at its slot's defence points: an idle builder places cover lines
   (`fieldWorkLines`) and optionally a barrier line (`fieldWorkBarrier`) with `CommandType.PlaceLine`. Mark a
   line-placed building with `AIFieldWorkAuthoring` (role Cover or Barrier) for the bot to use it; such a building is
   never a build-order step.
7. Once enough combat units are idle, attack-moves them along a lane to the nearest hostile building as one command
   (`PlayerCommands.Address`), so the wave moves as one formation. Squads that retreat or could be reinforced, and
   non-squad builders, stay out of it.

The lane is chosen from the match seed, so replays repeat it. To replace the AI, disable the system and write your own
`PlayerCommand`s.

## Audio

Part of `Cues`. `EntitySoundsAuthoring` gives an entity a `SoundCue` asset per cue slot. A `SoundCue` holds clips
(drawn at random, never the last one again), volume and pitch ranges, priority and 3D settings. Games add their own
slots from `GameFirst` up. A cue's `Variant` byte picks among one slot's entries: the weapon for Fire, the kind for
Announce.

| Slot | Plays | Heard by |
| --- | --- | --- |
| Fire, Impact, Death, Ability | Each shot, where it lands, on death, on an ability at the aim point | Whoever sees it |
| ProductionDone | At the producer when a unit or research finishes | Owner |
| Ready, Select, Move, Attack | Voice when produced or built, selected, ordered | Owner |
| AbilityVoice | Voice at the caster when it uses an ability | Owner |
| UnderFire | Voice when hit after a quiet spell | Owner |
| TakeCover, Suppressed, Pinned, Retreat, DigIn, Reinforced | Squad barks | Owner |

`SquadBarkSystem` plays squad barks on the client for the local player's squads, when a squad reaches cover, is
suppressed or pinned, retreats, digs in or gets a member back. Simultaneous reasons are ranked (Pinned first), and
per-squad and per-player intervals keep them rare. Those intervals and the acknowledgement gap are `VoiceSettings` on
`InfantryTuning.voice`, baked into `VoiceRules`. A squad's first appearance, such as a spawn, stays silent.

Playback is on the [client](../presentation/README.md#audio); the server plays nothing.

### Loops

`EntityLoopsAuthoring` (in `Presentation/Audio`) gives an entity continuous sounds driven by replicated state,
attached in 3D: `engineIdle` and `engineMove` crossfaded by speed, `rotor` following the
[motion rig](../presentation/animation.md#motion-rig)'s `RotorSpin`, `turretTraverse` while any weapon turns, `working` while a building
is under construction or a producer has a queue, and `ambient`. Loops fall silent when the entity dies, hides in fog or
leaves range, and only the nearest `maxInstances` of a cue play.

### Announcer

The simulation writes owner-only `Announce` cues whose `AnnouncementKind` is unit lost, base under attack, low power,
construction complete, unit ready or upgrade complete. A game numbers its own kinds from `AnnouncementKind.GameFirst`
(128) up and writes them with `CueWriter.Announce`. `AnnouncementGate` limits each player to one of a kind per second.

An `AnnouncerSet` asset lists a line per kind, a 2D `SoundCue` with its own cooldown; a kind without a line stays
silent. `AnnouncerAuthoring` in the match's subscene picks a set per `PlayerSide`. A game may rewrite the
`AnnouncerVoice` buffer on a client at runtime, for example for the chosen language. The client speaks alerts one at a
time, lowest cue priority first, and drops lines that wait longer than `maxWait`.

## Effects

Effects play on the same cue slots as sounds. Rigs, their authoring and playback are presentation, in
[effects](../presentation/effects.md).

## Cues

A cue is a one-off event to hear or see: a shot, an impact, a death. Simulation systems write cues with `CueWriter`
into one queue. Sounds, effects and impact marks all read it, and the network and replays carry it.

- A cue is only queued if its source has a sound or effect for the slot.
- Voices, production done and announcements reach their owner. Other cues reach whoever can see the source.
  `CueRules` holds this rule.
- Games append cues before the lifecycle phase ends. The queue is emptied each frame.

## Replays

Replays record what happened, not the commands, because the simulation is not deterministic.

- `ReplayRecorder` samples every game entity at 10 Hz, plus every cue except owner-only ones.
- `ReplaySerializer` saves and loads the file. Other format versions are rejected.
- `ReplayViewer` plays a replay in a local world with the fog revealed. It can seek.

## Strategic assets

Part of `Airdrops`. `StrategicAssetAuthoring` on a building turns one of its `AbilityAuthoring` entries, named by
`ability`, into a strategic asset. That entry targets a point, has range 0 and starts on cooldown: the cooldown is the
charge. The owner fires it from the command card like any building ability.

- The charge pauses while the building is offline and restarts for a new owner. `MatchRules.StrategicAssets` (set by
  `MatchSetup.StrategicAssets`, on by default) switches the whole feature.
- Each player's `StrategicCharge` replicates on the player ghost, so every client shows the map-wide countdown. Losing
  the building clears it.
- Firing sends the `aircraft` in from the owner's map edge on a `DropRoute`. It carries `StealthAuthoring` and loses
  its stealth `revealSeconds` before the drop, so fighters and SAMs can engage it. Shot down first, the strike is lost.
- Over the target it drops a `BombLoad` from its release point, as an [airdrop](economy.md#airdrops) does:
  `count` guided bombs from the `bomb` prefab that seek enemy structures within `zoneRadius`, nearest first, or fall in
  a line along the run when there are none. Each enemy weapon that can hit air and reaches the target removes one
  bomb, down to `maxReduction` of the stick.
