# Support modules

[Modules](../modules.md) ▸ AI, Cues, Replays, Animation

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
volume and pitch ranges, priority and 3D settings. Slots are Fire, Impact, Death and Ability, plus the voice slots
Ready, Select, Move and Attack that only the owner hears. Games add their own slots from `Custom` up.

Sounds play through [cues](#cues). Playback is on the [client](client.md#presentation).

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

Rigs play through [cues](#cues), on the client.

## Cues

A cue is a one-off event to hear or see: a shot, an impact, a death. Simulation systems write cues with `CueWriter`
into one queue. Sounds, effects and impact marks all read it, and the network and replays carry it.

- A cue is only queued if its source has a sound or effect for the slot.
- Voices reach their owner. Other cues reach whoever can see the source. `CueRules` holds this rule.
- Games append cues before the lifecycle phase ends. The queue is emptied each frame.
- A unit says a voice line at most once a second.

## Replays

Replays record what happened, not the commands, because the simulation is not deterministic.

- `ReplayRecorder` samples every game entity at 10 Hz, plus every cue except voices.
- `ReplaySerializer` saves and loads the file. Other format versions are rejected.
- `ReplayViewer` plays a replay in a local world with the fog revealed. It can seek.

## Animation

Animates map machines such as pumpjacks and wind turbines. Put `MachineAuthoring` on the root and
`MachinePartAuthoring` on each moving child. A part can `Spin`, `Swing` in step with the spin, or act as a `Link`
rod between two transforms.
