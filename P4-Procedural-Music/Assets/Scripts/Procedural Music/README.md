# Procedural Music System for Unity
## Based on Lerdahl's Tonal Pitch Space

A fully procedural, real-time music generation system for Unity that creates all audio
through synthesis — no pre-recorded samples required. Musical decisions are driven by
Lerdahl's Tonal Pitch Space (TPS) model, providing theoretically grounded harmony,
melody, and tension management controllable from gameplay.

---

## Quick Start

### Scene Setup
1. Create an empty GameObject named `ProceduralMusic`
2. Add an **AudioSource** component (leave AudioClip empty)
3. Add the **ProceduralMusicController** script
4. Optionally add **MusicTestController** for keyboard testing

### From Your Game Code
```csharp
// Get reference to the music controller
var music = FindObjectOfType<ProceduralMusicController>();

// Control tension continuously (0 = calm, 1 = intense)
music.SetTension(0.7f);

// Switch game states for structural changes
music.SetGameState(GameMusicState.Combat);

// Force a key change for dramatic effect
music.ForceModulation(PitchClass.D, MusicalMode.HarmonicMinor);

// Read musical state for visual sync
float musicalTension = music.GetCurrentMusicalTension();
Chord currentChord = music.GetCurrentChord();
```

---

## Architecture

```
┌─────────────────────────────────────────────────────────┐
│  GAME LOGIC                                              │
│  SetTension(float) / SetGameState(GameMusicState)       │
└──────────────────────┬──────────────────────────────────┘
                       │
┌──────────────────────▼──────────────────────────────────┐
│  BRIDGE: ProceduralMusicController (MonoBehaviour)       │
│  - Maps game states to musical parameters                │
│  - Manages note scheduling and timing                    │
│  - Routes OnAudioFilterRead to MasterMixer               │
└──────────────────────┬──────────────────────────────────┘
                       │
┌──────────────────────▼──────────────────────────────────┐
│  COMPOSITION ENGINE                                      │
│  ┌─────────────────┐ ┌──────────────┐ ┌──────────────┐ │
│  │ ChordProgression│ │ Melody       │ │ Bass         │ │
│  │ Generator       │ │ Generator    │ │ Generator    │ │
│  │ (TPS tension)   │ │ (TPS attract)│ │ (root+walk)  │ │
│  └────────┬────────┘ └──────┬───────┘ └──────┬───────┘ │
│  ┌────────▼─────────────────▼────────────────▼───────┐ │
│  │ Rhythm Generator (Euclidean rhythms)               │ │
│  └───────────────────────────────────────────────────┘ │
└──────────────────────┬──────────────────────────────────┘
                       │ NoteEvents
┌──────────────────────▼──────────────────────────────────┐
│  SYNTH ENGINE                                            │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌───────────┐  │
│  │ Pad      │ │ Lead     │ │ Bass     │ │ Percussion│  │
│  │ Additive │ │ FM Synth │ │ Subtract.│ │ Noise+Sin │  │
│  └────┬─────┘ └────┬─────┘ └────┬─────┘ └─────┬─────┘  │
│       └─────────────┴────────────┴─────────────┘        │
│                    MasterMixer                           │
│              (soft clip + reverb)                        │
└──────────────────────┬──────────────────────────────────┘
                       │ float[] audio buffer
                       ▼
                 Unity AudioSource
```

---

## Module Reference

### Core: Tonal Pitch Space (`TonalPitchSpace.cs`)
Implements Lerdahl's TPS hierarchy:
- **Level d (root):** Root of the current chord — maximum stability
- **Level c (triadic):** The 3 chord tones
- **Level b (diatonic):** The 7 scale degrees
- **Level a (chromatic):** All 12 pitch classes

Key functions:
- `ChordDistance(from, to, key)` — How "far apart" two chords are
- `KeyDistance(from, to)` — Distance between keys (for modulations)
- `GetTension(chord, key)` — Tension rating of a chord in context
- `GetAttraction(pitch, chord, key)` — Melodic pull toward stable pitches
- `SuggestNextChords(current, key, targetTension)` — TPS-guided chord suggestions

### Synthesis (`SynthEngine.cs`, `VoiceManager.cs`)
- **Oscillator:** Sine, saw, square, triangle, noise with PolyBLEP antialiasing
- **ADSREnvelope:** Attack-decay-sustain-release with exponential curves
- **LowPassFilter:** State variable filter with resonance
- **FMOperator:** 2-operator FM with dynamic modulation index
- **SynthVoice:** Complete voice (oscillator + envelope + filter)
- **VoiceManager:** Polyphonic voice allocation with voice stealing
- **MasterMixer:** Stereo mixing, comb reverb, soft-clip limiter

### Composition (`CompositionEngine.cs`)
- **ChordProgressionGenerator:** TPS-driven chord selection
- **MelodyGenerator:** Attraction-weighted pitch selection with stepwise bias
- **BassGenerator:** Root-fifth patterns with walking bass at high tension
- **RhythmGenerator:** Euclidean rhythm patterns for percussion

### Bridge (`ProceduralMusicController.cs`)
- **ProceduralMusicController:** MonoBehaviour with game-facing API
- **GameMusicState:** Preset musical configurations per game state
- **MusicTestController:** Keyboard-driven test harness

---

## Game States

| State     | Mode           | Tempo     | Instruments          | Character           |
|-----------|---------------|-----------|----------------------|---------------------|
| Explore   | Major         | 85-105    | Pad, Melody, Bass    | Open, wandering     |
| Dialogue  | Major         | 70-90     | Pad only             | Quiet, unobtrusive  |
| Tension   | Natural Minor | 95-120    | All                  | Building unease     |
| Combat    | Harmonic Minor| 130-160   | All (bright FM)      | Intense, driving    |
| Victory   | Major         | 110-130   | All                  | Triumphant          |
| Mystery   | Dorian        | 75-95     | Pad, Melody          | Enigmatic           |
| Ambient   | Major         | 60-80     | Pad only             | Atmospheric         |

---

## Customization

### Adding New Instrument Presets
Create a new `InstrumentPreset` and add it to the mixer:
```csharp
var bellPreset = new InstrumentPreset
{
    Name = "Bell",
    SynthType = SynthVoice.SynthType.FM,
    MaxPolyphony = 4,
    Attack = 0.001f, Decay = 1.5f, Sustain = 0f, Release = 2f,
    FMRatio = 3.5f, FMIndex = 4f, FMEnvAmount = 0.9f,
    Volume = 0.3f
};
```

### Custom Game State Configs
Override defaults in `ProceduralMusicController.Initialize()` or create
your own `MusicStateConfig` dictionaries.

### Seeding for Reproducibility
Pass a seed to `CompositionEngine` for deterministic music generation:
```csharp
var composer = new CompositionEngine(key, seed: 42);
```

---

## Performance Notes

- All synthesis runs on the audio thread via `OnAudioFilterRead`
- Typical CPU: ~2-5% on modern hardware (6 instruments, ~20 voices)
- No allocations during audio generation (pre-allocated buffers)
- Composition runs on the main thread via `Update()` (lightweight)
- Voice stealing prevents runaway polyphony

---

## Theory: Why TPS?

Most procedural music systems use ad-hoc rules or Markov chains for harmony.
Lerdahl's Tonal Pitch Space provides a psychoacoustically grounded model that:

1. **Quantifies tension:** Distance from the tonic = perceived tension
2. **Predicts melodic pull:** The attraction model explains why some notes
   "want" to resolve to others
3. **Enables principled modulation:** Key distance guides smooth transitions
4. **Scales with complexity:** The same framework handles simple triads
   and complex chromatic chords

The result: music that doesn't just follow rules, but follows the perceptual
logic of how listeners actually experience tonal music.
