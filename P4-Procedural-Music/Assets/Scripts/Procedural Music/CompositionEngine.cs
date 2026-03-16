using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ProceduralMusic.Core;

namespace ProceduralMusic.Composition
{
    /// <summary>
    /// Musical event representing a note to be played at a specific beat offset.
    /// BeatOffset is relative to the start of the current chord/measure.
    /// </summary>
    public struct NoteEvent
    {
        public int MidiNote;
        public float Velocity;        // 0-1
        public float DurationBeats;    // How long the note sustains
        public int InstrumentIndex;    // Which instrument to play on
        public float BeatOffset;       // When to trigger (beats from start of measure)

        public NoteEvent(int midiNote, float velocity, float durationBeats, int instrumentIndex,
            float beatOffset = 0f)
        {
            MidiNote = midiNote;
            Velocity = velocity;
            DurationBeats = durationBeats;
            InstrumentIndex = instrumentIndex;
            BeatOffset = beatOffset;
        }
    }

    /// <summary>
    /// Generates chord progressions driven by TPS tension model.
    /// </summary>
    public class ChordProgressionGenerator
    {
        public Key CurrentKey;
        public Chord CurrentChord;
        public int BeatsPerChord = 4;

        private System.Random _rng;

        public ChordProgressionGenerator(Key key, int seed = -1)
        {
            CurrentKey = key;
            CurrentChord = new Chord(key.Root,
                (key.Mode == MusicalMode.Major || key.Mode == MusicalMode.Mixolydian)
                    ? ChordQuality.Major : ChordQuality.Minor);
            _rng = seed >= 0 ? new System.Random(seed) : new System.Random();
        }

        public Chord GetNextChord(float targetTension)
        {
            var candidates = TonalPitchSpace.SuggestNextChords(CurrentChord, CurrentKey, targetTension, 4);

            if (candidates.Count == 0)
            {
                CurrentChord = new Chord(CurrentKey.Root, ChordQuality.Major);
                return CurrentChord;
            }

            float[] weights = new float[candidates.Count];
            float totalWeight = 0f;
            for (int i = 0; i < candidates.Count; i++)
            {
                weights[i] = Mathf.Pow(0.5f, i);
                totalWeight += weights[i];
            }

            float roll = (float)_rng.NextDouble() * totalWeight;
            float cumulative = 0f;
            for (int i = 0; i < candidates.Count; i++)
            {
                cumulative += weights[i];
                if (roll <= cumulative)
                {
                    CurrentChord = candidates[i];
                    return CurrentChord;
                }
            }

            CurrentChord = candidates[0];
            return CurrentChord;
        }

        public void Modulate(Key newKey)
        {
            CurrentKey = newKey;
        }
    }

    /// <summary>
    /// Generates melodies using TPS attraction weights with proper rhythmic placement.
    /// Notes are placed at musical subdivisions across the measure, not all at beat 1.
    /// Uses rhythmic motifs and syncopation for natural-sounding phrases.
    /// </summary>
    public class MelodyGenerator
    {
        public int OctaveMin = 4;
        public int OctaveMax = 5;
        public float RestProbability = 0.15f;
        public float StepwiseMotionBias = 0.8f;     // Strong preference for smooth motion
        public float SyncopationAmount = 0.12f;
        public float TiedNoteProbability = 0.2f;     // More legato ties for singing quality
        public float LeapRecoveryBias = 0.9f;        // After a leap, strongly prefer stepping back

        private int _lastPitch = -1;
        private int _lastInterval = 0;               // Track last interval for contour shaping
        private System.Random _rng;

        // Rhythmic pattern library: beat offsets within a 4-beat measure.
        // Includes longer-note patterns for more melodic phrasing.
        private readonly float[][] _rhythmPatterns = new float[][]
        {
            // Simple quarter notes
            new float[] { 0f, 1f, 2f, 3f },
            // Dotted: long-short feel (very melodic)
            new float[] { 0f, 1.5f, 2f, 3f, 3.5f },
            // Syncopated: off-beat emphasis
            new float[] { 0f, 0.5f, 1.5f, 2f, 3f, 3.5f },
            // Sparse: breathing room (two long notes)
            new float[] { 0f, 2f },
            // Driving eighth notes
            new float[] { 0f, 0.5f, 1f, 1.5f, 2f, 2.5f, 3f, 3.5f },
            // Lilting triplet feel
            new float[] { 0f, 0.67f, 1f, 2f, 2.67f, 3f },
            // Call and response: phrase then space
            new float[] { 0f, 0.5f, 1f, 1.5f, 3f },
            // Gentle long phrases (half notes)
            new float[] { 0f, 2f, 3f },
            // Offbeat singing
            new float[] { 0.5f, 1.5f, 2.5f, 3.5f },
            // Mixed: run then sustain
            new float[] { 0f, 0.25f, 0.5f, 1f, 2.5f },
            // Two long notes with pickup
            new float[] { 0f, 1.5f, 3.5f },
            // Sustained single note (leave space for other instruments)
            new float[] { 0f, 3f },
        };

        public MelodyGenerator(int seed = -1)
        {
            _rng = seed >= 0 ? new System.Random(seed) : new System.Random();
        }

        public List<NoteEvent> GeneratePhrase(Chord chord, Key key, float tension, int beatsPerChord,
            int instrumentIndex)
        {
            var events = new List<NoteEvent>();
            int[] scalePCs = key.GetScalePitchClasses();
            int[] chordPCs = chord.GetPitchClasses();

            var candidates = BuildCandidates(chord, key, tension, scalePCs, chordPCs);
            float[] beatPositions = SelectRhythmPattern(tension, beatsPerChord);

            for (int i = 0; i < beatPositions.Length; i++)
            {
                float beatPos = beatPositions[i];
                if (beatPos >= beatsPerChord) continue;

                // Rests: fewer at high tension for more activity
                float restChance = Mathf.Lerp(RestProbability * 1.5f, RestProbability * 0.5f, tension);
                if (i > 0 && (float)_rng.NextDouble() < restChance)
                    continue;

                // Syncopation: slight timing shift
                if ((float)_rng.NextDouble() < SyncopationAmount && i > 0)
                {
                    float shift = ((float)_rng.NextDouble() - 0.5f) * 0.25f;
                    beatPos = Mathf.Max(0f, beatPos + shift);
                }

                // Duration: time until next note or end of measure
                float nextBeat = (i + 1 < beatPositions.Length) ? beatPositions[i + 1] : beatsPerChord;
                float maxDuration = nextBeat - beatPos;

                float durationFactor;
                if ((float)_rng.NextDouble() < TiedNoteProbability)
                    durationFactor = 1.0f;
                else if ((float)_rng.NextDouble() < 0.3f)
                    durationFactor = 0.3f + (float)_rng.NextDouble() * 0.2f;
                else
                    durationFactor = 0.6f + (float)_rng.NextDouble() * 0.3f;

                float duration = Mathf.Max(maxDuration * durationFactor, 0.1f);

                // Pitch selection
                int selectedMidi = SelectPitch(candidates, i, beatPositions.Length, chordPCs, scalePCs,
                    beatPos, beatsPerChord);

                // Velocity: clear accent pattern, capped so melody doesn't overwhelm
                float velocity = Mathf.Lerp(0.45f, 0.75f, tension); // Capped at 0.75 not 0.85
                bool isDownbeat = beatPos < 0.05f;
                bool isStrongBeat = (beatPos % 1f < 0.05f) && (Mathf.RoundToInt(beatPos) % 2 == 0);
                if (isDownbeat) velocity *= 1.1f;
                else if (isStrongBeat) velocity *= 0.95f;
                else velocity *= 0.7f + (float)_rng.NextDouble() * 0.15f;
                velocity *= 0.92f + (float)_rng.NextDouble() * 0.16f;

                events.Add(new NoteEvent(selectedMidi, Mathf.Clamp01(velocity), duration,
                    instrumentIndex, beatPos));
                _lastPitch = selectedMidi;
            }

            return events;
        }

        private float[] SelectRhythmPattern(float tension, int beatsPerChord)
        {
            List<float[]> pool = new List<float[]>();

            if (tension < 0.3f)
            {
                // Low tension: long, singing notes
                pool.Add(_rhythmPatterns[3]);  // Two long notes
                pool.Add(_rhythmPatterns[7]);  // Half note feel
                pool.Add(_rhythmPatterns[10]); // Two longs with pickup
                pool.Add(_rhythmPatterns[11]); // Sustained single
            }
            else if (tension < 0.6f)
            {
                // Medium: melodic quarter/dotted patterns
                pool.Add(_rhythmPatterns[0]);  // Simple quarters
                pool.Add(_rhythmPatterns[1]);  // Dotted (very melodic)
                pool.Add(_rhythmPatterns[5]);  // Triplet feel
                pool.Add(_rhythmPatterns[8]);  // Offbeat singing
                pool.Add(_rhythmPatterns[6]);  // Call and response
            }
            else
            {
                // High tension: denser, more active
                pool.Add(_rhythmPatterns[2]);  // Syncopated
                pool.Add(_rhythmPatterns[4]);  // Eighth notes
                pool.Add(_rhythmPatterns[9]);  // Run then sustain
                pool.Add(_rhythmPatterns[6]);  // Call and response
            }

            float[] pattern = pool[_rng.Next(pool.Count)];

            if (beatsPerChord != 4)
            {
                float scale = beatsPerChord / 4f;
                pattern = pattern.Select(b => b * scale).Where(b => b < beatsPerChord).ToArray();
            }

            return pattern;
        }

        private List<(int midiNote, float weight)> BuildCandidates(Chord chord, Key key, float tension,
            int[] scalePCs, int[] chordPCs)
        {
            var candidates = new List<(int midiNote, float weight)>();
            for (int octave = OctaveMin; octave <= OctaveMax; octave++)
            {
                foreach (int pc in scalePCs)
                {
                    int midi = (octave + 1) * 12 + pc;
                    float attraction = TonalPitchSpace.GetAttraction(pc, chord, key);
                    float stability = TonalPitchSpace.GetPitchStability(pc, chordPCs, scalePCs, (int)key.Root);

                    float weight = stability * 0.6f + attraction * 0.4f;
                    if (tension > 0.5f)
                        weight = stability * 0.3f + attraction * 0.7f + (4f - stability) * tension * 0.3f;

                    candidates.Add((midi, Mathf.Max(weight, 0.1f)));
                }
            }
            return candidates;
        }

        private int SelectPitch(List<(int midiNote, float weight)> candidates,
            int noteIndex, int totalNotes, int[] chordPCs, int[] scalePCs,
            float beatPos, int beatsPerChord)
        {
            var adjusted = candidates.Select(c =>
            {
                float w = c.weight;

                if (_lastPitch > 0)
                {
                    int interval = c.midiNote - _lastPitch;
                    int distance = Mathf.Abs(interval);

                    // Stepwise motion preference
                    if (distance <= 2) w *= 1f + StepwiseMotionBias;
                    else if (distance <= 4) w *= 0.9f;
                    else if (distance <= 7) w *= 0.4f;
                    else w *= 0.1f;

                    // Leap recovery: if last interval was a leap (>4 semitones),
                    // strongly prefer stepping back in the opposite direction.
                    // This is a fundamental rule of melodic writing.
                    if (Mathf.Abs(_lastInterval) > 4)
                    {
                        bool isRecovery = (interval * _lastInterval) < 0; // Opposite direction
                        bool isStep = distance <= 3;
                        if (isRecovery && isStep)
                            w *= LeapRecoveryBias * 2f; // Strongly favor recovery step
                        else if (!isRecovery && distance > 2)
                            w *= 0.2f; // Penalize continuing in leap direction
                    }

                    // Phrase contour: first half trends upward, second half trends downward
                    // Creates natural arch shape (like a sung phrase)
                    float phrasePosition = (float)noteIndex / Mathf.Max(totalNotes - 1, 1);
                    if (phrasePosition < 0.5f)
                    {
                        // Rising phase: slight preference for upward motion
                        if (interval > 0 && distance <= 4) w *= 1.2f;
                    }
                    else
                    {
                        // Falling phase: slight preference for downward motion
                        if (interval < 0 && distance <= 4) w *= 1.2f;
                    }
                }

                // Strong beats: favor chord tones
                if (beatPos % 1f < 0.05f && chordPCs.Contains(c.midiNote % 12))
                    w *= 1.5f;

                // Phrase start/end: chord tones for coherence
                if ((noteIndex == 0 || noteIndex == totalNotes - 1) && chordPCs.Contains(c.midiNote % 12))
                    w *= 2f;

                // Near measure end: pull toward chord tones for resolution
                if (beatPos > beatsPerChord * 0.8f && chordPCs.Contains(c.midiNote % 12))
                    w *= 1.3f;

                return (c.midiNote, w);
            }).ToList();

            float totalWeight = adjusted.Sum(a => a.w);
            float roll = (float)_rng.NextDouble() * totalWeight;
            float cumulative = 0f;

            foreach (var (midiNote, w) in adjusted)
            {
                cumulative += w;
                if (roll <= cumulative)
                {
                    // Track interval for leap recovery on next note
                    if (_lastPitch > 0)
                        _lastInterval = midiNote - _lastPitch;
                    return midiNote;
                }
            }

            return adjusted[0].midiNote;
        }
    }

    /// <summary>
    /// Generates bass lines with proper rhythmic placement.
    /// Bass notes walk, groove, and syncopate depending on tension.
    /// </summary>
    public class BassGenerator
    {
        public int Octave = 2;

        private System.Random _rng;
        private int _lastNote = -1;

        // Bass rhythm templates (beat offsets within 4 beats)
        private readonly float[][] _bassPatterns = new float[][]
        {
            // Whole note: just the root, sustained
            new float[] { 0f },
            // Half notes
            new float[] { 0f, 2f },
            // Quarter note groove
            new float[] { 0f, 1f, 2f, 3f },
            // Bouncing: root with off-beat ghost
            new float[] { 0f, 1.5f, 2f, 3.5f },
            // Driving eighth notes
            new float[] { 0f, 0.5f, 1f, 1.5f, 2f, 2.5f, 3f, 3.5f },
            // Syncopated funk
            new float[] { 0f, 0.75f, 1.5f, 2f, 2.75f, 3.5f },
            // Off-beat emphasis
            new float[] { 0.5f, 1.5f, 2.5f, 3.5f },
            // Dotted quarter feel
            new float[] { 0f, 1.5f, 3f },
        };

        public BassGenerator(int seed = -1)
        {
            _rng = seed >= 0 ? new System.Random(seed) : new System.Random();
        }

        public List<NoteEvent> GeneratePhrase(Chord chord, Key key, float tension, int beatsPerChord,
            int instrumentIndex)
        {
            var events = new List<NoteEvent>();
            int root = TonalPitchSpace.ToMidi(chord.Root, Octave);
            int fifth = root + 7;
            int third = root + (chord.Quality == ChordQuality.Minor ||
                                chord.Quality == ChordQuality.Minor7 ||
                                chord.Quality == ChordQuality.Diminished ? 3 : 4);
            int octaveUp = root + 12;
            int[] scalePCs = key.GetScalePitchClasses();

            float[] pattern = SelectBassPattern(tension, beatsPerChord);

            for (int i = 0; i < pattern.Length; i++)
            {
                float beatPos = pattern[i];
                if (beatPos >= beatsPerChord) continue;

                float nextBeat = (i + 1 < pattern.Length) ? pattern[i + 1] : beatsPerChord;
                float duration = Mathf.Max((nextBeat - beatPos) * 0.85f, 0.15f);

                int note = ChooseBassPitch(i, pattern.Length, beatPos, beatsPerChord,
                    root, third, fifth, octaveUp, scalePCs, tension);

                float velocity;
                if (beatPos < 0.05f)
                    velocity = 0.85f;
                else if (beatPos % 1f < 0.05f)
                    velocity = 0.7f;
                else
                    velocity = 0.5f + (float)_rng.NextDouble() * 0.15f;

                velocity *= Mathf.Lerp(0.7f, 1f, tension);

                events.Add(new NoteEvent(note, Mathf.Clamp01(velocity), duration,
                    instrumentIndex, beatPos));
                _lastNote = note;
            }

            return events;
        }

        private float[] SelectBassPattern(float tension, int beatsPerChord)
        {
            List<float[]> pool = new List<float[]>();

            if (tension < 0.25f)
            {
                pool.Add(_bassPatterns[0]); // Whole note
                pool.Add(_bassPatterns[1]); // Half notes
            }
            else if (tension < 0.5f)
            {
                pool.Add(_bassPatterns[1]); // Half notes
                pool.Add(_bassPatterns[2]); // Quarters
                pool.Add(_bassPatterns[7]); // Dotted quarter
                pool.Add(_bassPatterns[3]); // Bouncing
            }
            else if (tension < 0.75f)
            {
                pool.Add(_bassPatterns[2]); // Quarters
                pool.Add(_bassPatterns[3]); // Bouncing
                pool.Add(_bassPatterns[7]); // Dotted quarter
            }
            else
            {
                // High tension: driving quarters and syncopation, locked to root/fifth
                pool.Add(_bassPatterns[2]); // Quarters
                pool.Add(_bassPatterns[5]); // Syncopated
                pool.Add(_bassPatterns[3]); // Bouncing
            }

            float[] pattern = pool[_rng.Next(pool.Count)];

            if (beatsPerChord != 4)
            {
                float scale = beatsPerChord / 4f;
                pattern = pattern.Select(b => b * scale).Where(b => b < beatsPerChord).ToArray();
            }

            return pattern;
        }

        private int ChooseBassPitch(int noteIndex, int totalNotes, float beatPos, int beatsPerChord,
            int root, int third, int fifth, int octaveUp, int[] scalePCs, float tension)
        {
            // First note on the downbeat is always the root
            if (noteIndex == 0 && beatPos < 0.1f)
                return root;

            // Last note: approach next chord via fifth or leading tone
            if (noteIndex == totalNotes - 1 && beatPos > beatsPerChord * 0.7f)
            {
                float r = (float)_rng.NextDouble();
                if (r < 0.4f) return fifth;
                if (r < 0.6f) return root - 1;
                return root;
            }

            // At high tension: keep it simple — root and fifth only.
            // This anchors the harmony and lets drums/melody carry intensity.
            if (tension > 0.65f)
            {
                return ((float)_rng.NextDouble() < 0.6f) ? root : fifth;
            }

            // Strong beats: chord tones
            if (beatPos % 1f < 0.05f)
            {
                float r = (float)_rng.NextDouble();
                if (r < 0.45f) return root;
                if (r < 0.7f) return fifth;
                if (r < 0.85f) return third;
                return octaveUp;
            }

            // Weak beats at medium tension: walk between chord tones
            if (tension > 0.4f && _lastNote >= 0)
            {
                int direction = (_rng.Next(2) == 0) ? 1 : -1;
                int target = ((noteIndex + 1) % 2 == 0) ? root : fifth;
                if (_lastNote < target) direction = 1;
                else if (_lastNote > target) direction = -1;

                int candidate = _lastNote + direction * (_rng.Next(1, 3));
                return SnapToScale(candidate, scalePCs, Octave);
            }

            return root;
        }

        private int SnapToScale(int midiNote, int[] scalePCs, int baseOctave)
        {
            int pc = midiNote % 12;
            int octave = midiNote / 12 - 1;
            int minDist = 12;
            int bestPC = pc;
            foreach (int spc in scalePCs)
            {
                int dist = Mathf.Min(Mathf.Abs(pc - spc), 12 - Mathf.Abs(pc - spc));
                if (dist < minDist) { minDist = dist; bestPC = spc; }
            }
            return (octave + 1) * 12 + bestPC;
        }
    }

    /// <summary>
    /// Generates rhythm patterns using Euclidean algorithms with proper beat offsets.
    /// </summary>
    public class RhythmGenerator
    {
        private System.Random _rng;

        public RhythmGenerator(int seed = -1)
        {
            _rng = seed >= 0 ? new System.Random(seed) : new System.Random();
        }

        public List<NoteEvent> GeneratePattern(float tension, int beatsPerChord,
            int kickInst, int snareInst, int hihatInst)
        {
            var events = new List<NoteEvent>();
            int steps = 16;
            float stepDuration = (float)beatsPerChord / steps;

            // Kick: more onsets at higher tension, capped at 4
            int kickOnsets = Mathf.RoundToInt(Mathf.Lerp(2, 4, tension));
            bool[] kickPattern = EuclideanRhythm(kickOnsets, steps);

            // Snare: backbeat always, ghost notes add groove at higher tension
            bool[] snarePattern = new bool[steps];
            snarePattern[4] = true;   // Beat 2
            snarePattern[12] = true;  // Beat 4
            if (tension > 0.6f)
                snarePattern[10] = true; // Ghost note
            if (tension > 0.85f)
                snarePattern[6] = true;  // Extra ghost

            // Hi-hat: straight subdivisions, denser at high tension
            bool[] hihatPattern = new bool[steps];
            if (tension < 0.3f)
            {
                // Quarter notes
                for (int s = 0; s < steps; s += 4) hihatPattern[s] = true;
            }
            else if (tension < 0.6f)
            {
                // Eighth notes
                for (int s = 0; s < steps; s += 2) hihatPattern[s] = true;
            }
            else
            {
                // Sixteenth notes — driving
                for (int s = 0; s < steps; s++) hihatPattern[s] = true;
            }

            for (int step = 0; step < steps; step++)
            {
                float beatPos = step * stepDuration;

                if (kickPattern[step])
                {
                    float vel = (step == 0) ? 0.95f : 0.75f;
                    events.Add(new NoteEvent(36, vel, stepDuration, kickInst, beatPos));
                }

                if (snarePattern[step])
                {
                    bool isBackbeat = (step == 4 || step == 12);
                    float vel = isBackbeat ? 0.85f : 0.3f;
                    events.Add(new NoteEvent(38, vel, stepDuration, snareInst, beatPos));
                }

                if (hihatPattern[step])
                {
                    float vel;
                    if (step % 4 == 0) vel = 0.6f;
                    else if (step % 2 == 0) vel = 0.42f;
                    else vel = 0.25f;

                    // Open hi-hat on "and" of 2 and 4 for groove at higher tension
                    int note = (tension > 0.5f && (step == 6 || step == 14)) ? 46 : 42;
                    events.Add(new NoteEvent(note, vel, stepDuration * 0.4f, hihatInst, beatPos));
                }
            }

            return events;
        }

        public static bool[] EuclideanRhythm(int onsets, int steps)
        {
            if (onsets >= steps) return Enumerable.Repeat(true, steps).ToArray();
            if (onsets <= 0) return new bool[steps];

            var pattern = new List<List<bool>>();
            var remainder = new List<List<bool>>();

            for (int i = 0; i < onsets; i++) pattern.Add(new List<bool> { true });
            for (int i = 0; i < steps - onsets; i++) remainder.Add(new List<bool> { false });

            while (remainder.Count > 1)
            {
                int min = Mathf.Min(pattern.Count, remainder.Count);
                var newPattern = new List<List<bool>>();
                for (int i = 0; i < min; i++)
                {
                    var combined = new List<bool>(pattern[i]);
                    combined.AddRange(remainder[i]);
                    newPattern.Add(combined);
                }
                var newRemainder = new List<List<bool>>();
                for (int i = min; i < pattern.Count; i++) newRemainder.Add(pattern[i]);
                for (int i = min; i < remainder.Count; i++) newRemainder.Add(remainder[i]);
                pattern = newPattern;
                remainder = newRemainder;
            }

            var result = new List<bool>();
            foreach (var p in pattern) result.AddRange(p);
            foreach (var r in remainder) result.AddRange(r);
            return result.ToArray();
        }
    }

    /// <summary>
    /// Top-level composition engine that coordinates all generators
    /// and produces a time-stamped stream of note events.
    /// 
    /// The engine maintains an internal beat clock. At each chord boundary,
    /// it generates events for the upcoming measure with BeatOffset values.
    /// The bridge scheduler picks them up and triggers them at the right time.
    /// </summary>
    public class CompositionEngine
    {
        public ChordProgressionGenerator Chords;
        public MelodyGenerator Melody;
        public BassGenerator Bass;
        public RhythmGenerator Rhythm;

        public float Tempo = 120f;
        public int BeatsPerChord = 4;
        public float TensionTarget = 0.3f;
        public Key CurrentKey;

        public int PadInstrumentIndex = 0;
        public int LeadInstrumentIndex = 1;
        public int BassInstrumentIndex = 2;
        public int KickInstrumentIndex = 3;
        public int SnareInstrumentIndex = 4;
        public int HiHatInstrumentIndex = 5;
        public int StringsInstrumentIndex = 6;

        public bool EnablePad = true;
        public bool EnableMelody = true;
        public bool EnableBass = true;
        public bool EnablePercussion = true;
        public bool EnableStrings = true;

        // Scheduled events queue with absolute beat timestamps
        private List<(float absoluteBeat, NoteEvent noteEvent)> _pendingNoteOns
            = new List<(float, NoteEvent)>();
        private List<(float absoluteBeat, int midiNote, int instIndex)> _pendingNoteOffs
            = new List<(float, int, int)>();

        private float _currentBeat;
        private float _nextChordBeat;
        private float _smoothedTension;
        private bool _firstUpdate = true;

        public float CurrentBeat => _currentBeat;

        public CompositionEngine(Key key, int seed = -1)
        {
            CurrentKey = key;
            Chords = new ChordProgressionGenerator(key, seed);
            Melody = new MelodyGenerator(seed >= 0 ? seed + 1 : -1);
            Bass = new BassGenerator(seed >= 0 ? seed + 2 : -1);
            Rhythm = new RhythmGenerator(seed >= 0 ? seed + 3 : -1);
            _nextChordBeat = 0f;
        }

        /// <summary>
        /// Advance the beat clock and return events that should trigger this frame.
        /// </summary>
        public void Update(float deltaTime,
            out List<NoteEvent> noteOns,
            out List<(int midiNote, int instIndex)> noteOffs)
        {
            noteOns = new List<NoteEvent>();
            noteOffs = new List<(int, int)>();

            _smoothedTension = Mathf.Lerp(_smoothedTension, TensionTarget, deltaTime * 2f);

            float beatsPerSecond = Tempo / 60f;
            float previousBeat = _currentBeat;
            _currentBeat += deltaTime * beatsPerSecond;

            // Generate new measure at chord boundaries
            if (_currentBeat >= _nextChordBeat || _firstUpdate)
            {
                _firstUpdate = false;
                GenerateMeasure();
                _nextChordBeat += BeatsPerChord;
            }

            // Fire note-ons that fall within [previousBeat, currentBeat)
            for (int i = _pendingNoteOns.Count - 1; i >= 0; i--)
            {
                var (absBeat, evt) = _pendingNoteOns[i];
                if (absBeat >= previousBeat && absBeat < _currentBeat)
                {
                    noteOns.Add(evt);
                    _pendingNoteOffs.Add((absBeat + evt.DurationBeats, evt.MidiNote, evt.InstrumentIndex));
                    _pendingNoteOns.RemoveAt(i);
                }
                else if (absBeat < previousBeat)
                {
                    _pendingNoteOns.RemoveAt(i); // Missed, clean up
                }
            }

            // Fire note-offs
            for (int i = _pendingNoteOffs.Count - 1; i >= 0; i--)
            {
                var (absBeat, midiNote, instIndex) = _pendingNoteOffs[i];
                if (absBeat < _currentBeat)
                {
                    noteOffs.Add((midiNote, instIndex));
                    _pendingNoteOffs.RemoveAt(i);
                }
            }

            // Safety: prevent unbounded list growth
            if (_pendingNoteOns.Count > 500)
                _pendingNoteOns.RemoveRange(0, _pendingNoteOns.Count - 200);
            if (_pendingNoteOffs.Count > 500)
                _pendingNoteOffs.RemoveRange(0, _pendingNoteOffs.Count - 200);
        }

        private void GenerateMeasure()
        {
            float measureStart = _nextChordBeat;

            Chord newChord = Chords.GetNextChord(_smoothedTension);

            // Pad: ALWAYS sustained at consistent volume — this is the harmonic glue.
            // Without it, high tension sounds thin and broken.
            if (EnablePad)
            {
                // Slight extra duration for overlap into next chord (smooth transition)
                float padDuration = BeatsPerChord + 0.15f;
                int[] notes = newChord.GetMidiNotes(3);
                foreach (int note in notes)
                {
                    var evt = new NoteEvent(note, 0.45f, padDuration, PadInstrumentIndex, 0f);
                    _pendingNoteOns.Add((measureStart, evt));
                }
            }

            // Melody: notes spread across the measure
            if (EnableMelody)
            {
                var melodyEvents = Melody.GeneratePhrase(
                    newChord, CurrentKey, _smoothedTension, BeatsPerChord, LeadInstrumentIndex);
                foreach (var evt in melodyEvents)
                    _pendingNoteOns.Add((measureStart + evt.BeatOffset, evt));
            }

            // Bass: groove-placed notes
            if (EnableBass)
            {
                var bassEvents = Bass.GeneratePhrase(
                    newChord, CurrentKey, _smoothedTension, BeatsPerChord, BassInstrumentIndex);
                foreach (var evt in bassEvents)
                    _pendingNoteOns.Add((measureStart + evt.BeatOffset, evt));
            }

            // Percussion: hits at exact 16th note positions
            if (EnablePercussion)
            {
                var percEvents = Rhythm.GeneratePattern(
                    _smoothedTension, BeatsPerChord,
                    KickInstrumentIndex, SnareInstrumentIndex, HiHatInstrumentIndex);
                foreach (var evt in percEvents)
                    _pendingNoteOns.Add((measureStart + evt.BeatOffset, evt));
            }

            // Strings: adapt articulation to tension while ALWAYS providing harmonic support
            if (EnableStrings)
            {
                int[] stringNotes = newChord.GetMidiNotes(4);

                if (_smoothedTension < 0.4f)
                {
                    // Low tension: long sustained strings, gentle staggered entry
                    for (int i = 0; i < stringNotes.Length; i++)
                    {
                        float stagger = i * 0.05f;
                        float vel = (i == 0 || i == stringNotes.Length - 1) ? 0.5f : 0.35f;
                        var evt = new NoteEvent(stringNotes[i], vel, BeatsPerChord - 0.1f,
                            StringsInstrumentIndex, stagger);
                        _pendingNoteOns.Add((measureStart + stagger, evt));
                    }
                }
                else if (_smoothedTension < 0.65f)
                {
                    // Medium tension: sustained but louder, more present
                    for (int i = 0; i < stringNotes.Length; i++)
                    {
                        float stagger = i * 0.03f;
                        float vel = 0.55f;
                        var evt = new NoteEvent(stringNotes[i], vel, BeatsPerChord - 0.1f,
                            StringsInstrumentIndex, stagger);
                        _pendingNoteOns.Add((measureStart + stagger, evt));
                    }
                }
                else
                {
                    // High tension: rhythmic pulsing on every beat — NOT staccato with gaps.
                    // Each hit sustains until the next hit, so harmony never drops out.
                    float stringVel = Mathf.Lerp(0.5f, 0.65f, _smoothedTension);
                    int hitsPerMeasure = BeatsPerChord; // One hit per beat
                    float hitSpacing = (float)BeatsPerChord / hitsPerMeasure;

                    for (int hit = 0; hit < hitsPerMeasure; hit++)
                    {
                        float beatPos = hit * hitSpacing;
                        float hitDur = hitSpacing + 0.1f; // Slight overlap into next hit
                        float hitVel = (hit == 0) ? stringVel : stringVel * 0.85f; // Beat 1 accented

                        foreach (int note in stringNotes)
                        {
                            _pendingNoteOns.Add((measureStart + beatPos,
                                new NoteEvent(note, hitVel, hitDur, StringsInstrumentIndex, beatPos)));
                        }
                    }
                }
            }
        }

        public void ChangeKey(Key newKey)
        {
            CurrentKey = newKey;
            Chords.Modulate(newKey);
        }
    }
}
