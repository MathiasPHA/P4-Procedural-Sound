using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ProceduralMusic.Core;
using ProceduralMusic.Bridge;

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

        public Chord GetNextChord(float targetTension, bool allowTritones = false)
        {
            var candidates = TonalPitchSpace.SuggestNextChords(
                CurrentChord, CurrentKey, targetTension, allowTritones, 4);

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
    /// Music style affects ALL instrument pattern choices per state.
    /// Each game state sets one style that shapes melody, bass, percussion,
    /// strings, and kantele behavior simultaneously.
    /// </summary>
    public enum MusicStyle
    {
        Folk,       // Flowing, singable — dotted rhythms, lilting triplets (Exploring)
        Tense,      // Syncopated, driving — eighth notes, offbeats (Pressure, Combat)
        Sparse,     // Minimal, atmospheric — long held notes, lots of silence (Spooky, Night)
        Triumphant, // Bold, march-like — strong downbeats, quarter notes (Exploring2)
        Horror      // Almost nothing — rare broken phrases, sub-bass drone (Horror)
    }

    /// <summary>
    /// Generates melodies using TPS attraction weights with proper rhythmic placement.
    /// </summary>
    public class MelodyGenerator
    {
        public int OctaveMin = 4;
        public int OctaveMax = 4;
        public float RestProbability = 0.15f;
        public float StepwiseMotionBias = 1.5f;
        public float SyncopationAmount = 0.12f;
        public float TiedNoteProbability = 0.2f;
        public float LeapRecoveryBias = 0.9f;
        public MusicStyle Style = MusicStyle.Folk;

        private int _lastPitch = -1;
        private int _lastInterval = 0;
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

            // Sparse style: high chance of skipping the entire measure (silence as music)
            // This creates the ambient feel where instruments play occasionally, not constantly
            if (Style == MusicStyle.Sparse)
            {
                float skipChance = 0.55f; // 55% of measures are pure silence
                if ((float)_rng.NextDouble() < skipChance)
                    return events; // Return empty — nothing plays this measure
            }

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
                // Velocity: loud and expressive when soloing (low tension),
                // softer and blending when part of the ensemble (high tension)
                float velocity = Mathf.Lerp(0.7f, 0.45f, tension); // Louder at LOW tension
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

            // Each MusicStyle has its own pool of rhythm patterns,
            // giving each game state a distinct melodic feel.
            switch (Style)
            {
                case MusicStyle.Folk:
                    // Flowing, singable — lilting dotted rhythms, triplets
                    if (tension < 0.3f)
                    {
                        pool.Add(_rhythmPatterns[3]);  // Two long notes
                        pool.Add(_rhythmPatterns[7]);  // Half note feel
                        pool.Add(_rhythmPatterns[11]); // Sustained single
                    }
                    else if (tension < 0.6f)
                    {
                        pool.Add(_rhythmPatterns[1]);  // Dotted (very melodic)
                        pool.Add(_rhythmPatterns[5]);  // Triplet feel
                        pool.Add(_rhythmPatterns[8]);  // Offbeat singing
                    }
                    else
                    {
                        pool.Add(_rhythmPatterns[0]);  // Quarters
                        pool.Add(_rhythmPatterns[1]);  // Dotted
                        pool.Add(_rhythmPatterns[6]);  // Call and response
                    }
                    break;

                case MusicStyle.Tense:
                    // Syncopated, driving — offbeats and eighth note runs
                    if (tension < 0.4f)
                    {
                        pool.Add(_rhythmPatterns[0]);  // Quarters
                        pool.Add(_rhythmPatterns[2]);  // Syncopated
                    }
                    else
                    {
                        pool.Add(_rhythmPatterns[2]);  // Syncopated
                        pool.Add(_rhythmPatterns[4]);  // Eighth notes
                        pool.Add(_rhythmPatterns[9]);  // Run then sustain
                        pool.Add(_rhythmPatterns[6]);  // Call and response
                    }
                    break;

                case MusicStyle.Sparse:
                    // Minimal, atmospheric — long notes with lots of silence
                    pool.Add(_rhythmPatterns[3]);  // Two long notes
                    pool.Add(_rhythmPatterns[11]); // Sustained single
                    pool.Add(_rhythmPatterns[10]); // Two longs with pickup
                    if (tension > 0.5f)
                        pool.Add(_rhythmPatterns[7]); // Half note feel
                    break;

                case MusicStyle.Triumphant:
                    // Bold, march-like — strong downbeats, confident
                    if (tension < 0.4f)
                    {
                        pool.Add(_rhythmPatterns[0]);  // Quarters (march)
                        pool.Add(_rhythmPatterns[7]);  // Half note feel
                    }
                    else
                    {
                        pool.Add(_rhythmPatterns[0]);  // Quarters
                        pool.Add(_rhythmPatterns[1]);  // Dotted
                        pool.Add(_rhythmPatterns[5]);  // Triplet (fanfare feel)
                    }
                    break;
            }

            if (pool.Count == 0)
                pool.Add(_rhythmPatterns[0]); // Fallback

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

            // At low tension the flute is the soloist — it can wander through scale tones freely.
            // At high tension other instruments are playing, so the flute should stick to chord tones
            // to blend with the ensemble rather than clash.
            // chordStrictness: 0 = free/melodic, 1 = locked to chord tones
            float chordStrictness = Mathf.Clamp01((tension - 0.2f) / 0.5f); // 0 below 0.2, 1 above 0.7

            for (int octave = OctaveMin; octave <= OctaveMax; octave++)
            {
                foreach (int pc in scalePCs)
                {
                    int midi = (octave + 1) * 12 + pc;
                    bool isChordTone = chordPCs.Contains(pc);

                    float weight;
                    if (isChordTone)
                    {
                        // Chord tones always have decent weight
                        weight = Mathf.Lerp(9f, 20f, chordStrictness);
                    }
                    else
                    {
                        // Scale tones: freely available at low tension, suppressed at high
                        weight = Mathf.Lerp(4f, 0.5f, chordStrictness);
                    }

                    candidates.Add((midi, weight));
                }
            }
            return candidates;
        }

        private int SelectPitch(List<(int midiNote, float weight)> candidates,
            int noteIndex, int totalNotes, int[] chordPCs, int[] scalePCs,
            float beatPos, int beatsPerChord)
        {
            if (_lastPitch <= 0)
            {
                int midMidi = (OctaveMin + 1) * 12 + (int)chordPCs[0] + 7;
                _lastPitch = midMidi;
            }

            var adjusted = candidates.Select(c =>
            {
                float w = c.weight;

                int interval = c.midiNote - _lastPitch;
                int distance = Mathf.Abs(interval);

                // Stepwise motion preference
                if (distance == 0) w *= 0.3f;
                else if (distance <= 2) w *= 1f + StepwiseMotionBias;
                else if (distance <= 3) w *= 0.6f;
                else if (distance <= 5) w *= 0.15f;
                else w *= 0.02f;

                // Leap recovery
                if (Mathf.Abs(_lastInterval) > 4)
                {
                    bool isRecovery = (interval * _lastInterval) < 0;
                    bool isStep = distance <= 3;
                    if (isRecovery && isStep)
                        w *= LeapRecoveryBias * 3f;
                    else if (!isRecovery && distance > 2)
                        w *= 0.05f;
                }

                // Phrase contour: arch shape
                float phrasePosition = (float)noteIndex / Mathf.Max(totalNotes - 1, 1);
                if (phrasePosition < 0.5f)
                {
                    if (interval > 0 && distance <= 3) w *= 1.3f;
                }
                else
                {
                    if (interval < 0 && distance <= 3) w *= 1.3f;
                }

                // Strong beats: favor chord tones
                if (beatPos % 1f < 0.05f && chordPCs.Contains(c.midiNote % 12))
                    w *= 2f;

                // Phrase start/end: chord tones for coherence
                if ((noteIndex == 0 || noteIndex == totalNotes - 1) && chordPCs.Contains(c.midiNote % 12))
                    w *= 2.5f;

                // Near measure end: resolve toward chord tones
                if (beatPos > beatsPerChord * 0.75f && chordPCs.Contains(c.midiNote % 12))
                    w *= 1.5f;

                return (c.midiNote, Mathf.Max(w, 0.001f));
            }).ToList();

            float totalWeight = adjusted.Sum(a => a.Item2);
            float roll = (float)_rng.NextDouble() * totalWeight;
            float cumulative = 0f;

            foreach (var (midiNote, w) in adjusted)
            {
                cumulative += w;
                if (roll <= cumulative)
                {
                    if (_lastPitch > 0)
                        _lastInterval = midiNote - _lastPitch;
                    return midiNote;
                }
            }

            return adjusted[0].Item1;
        }
    }

    /// <summary>
    /// Generates bass lines with proper rhythmic placement.
    /// Bass notes walk, groove, and syncopate depending on tension.
    /// </summary>
    public class BassGenerator
    {
        public int Octave = 2;
        public MusicStyle Style = MusicStyle.Folk;

        private System.Random _rng;
        private int _lastNote = -1;
        public bool RootOnly = false;

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

                // Rest probability: bass breathes more at low tension
                float restChance = Mathf.Lerp(0.18f, 0.04f, tension);
                if (i > 0 && (float)_rng.NextDouble() < restChance)
                    continue;

                float nextBeat = (i + 1 < pattern.Length) ? pattern[i + 1] : beatsPerChord;
                float duration = Mathf.Max((nextBeat - beatPos) * 0.85f, 0.15f);

                int note = ChooseBassPitch(i, pattern.Length, beatPos, beatsPerChord,
                    root, third, fifth, octaveUp, scalePCs, tension);

                float velocity;
                if (beatPos < 0.05f)
                    velocity = 0.68f;           // Was 0.85f — downbeat still punchy but not dominant
                else if (beatPos % 1f < 0.05f)
                    velocity = 0.54f;           // Was 0.7f
                else
                    velocity = 0.38f + (float)_rng.NextDouble() * 0.12f; // Was 0.5f + 0.15f

                velocity *= Mathf.Lerp(0.65f, 0.82f, tension); // Was Lerp(0.7f, 1f) — no longer peaks at full

                events.Add(new NoteEvent(note, Mathf.Clamp01(velocity), duration,
                    instrumentIndex, beatPos));
                _lastNote = note;
            }

            return events;
        }

        private float[] SelectBassPattern(float tension, int beatsPerChord)
        {
            List<float[]> pool = new List<float[]>();

            switch (Style)
            {
                case MusicStyle.Folk:
                    // Gentle, walking — dotted and half notes, bouncing at higher tension
                    if (tension < 0.3f)
                    {
                        pool.Add(_bassPatterns[0]); // Whole
                        pool.Add(_bassPatterns[7]); // Dotted quarter
                    }
                    else if (tension < 0.6f)
                    {
                        pool.Add(_bassPatterns[1]); // Half
                        pool.Add(_bassPatterns[7]); // Dotted
                        pool.Add(_bassPatterns[3]); // Bouncing
                    }
                    else
                    {
                        pool.Add(_bassPatterns[2]); // Quarters
                        pool.Add(_bassPatterns[3]); // Bouncing
                    }
                    break;

                case MusicStyle.Tense:
                    // Driving, locked groove
                    if (tension < 0.4f)
                    {
                        pool.Add(_bassPatterns[2]); // Quarters
                        pool.Add(_bassPatterns[3]); // Bouncing
                    }
                    else
                    {
                        pool.Add(_bassPatterns[2]); // Quarters
                        pool.Add(_bassPatterns[5]); // Syncopated
                        pool.Add(_bassPatterns[4]); // Eighths
                    }
                    break;

                case MusicStyle.Sparse:
                    // Minimal — whole notes and long holds
                    pool.Add(_bassPatterns[0]); // Whole
                    pool.Add(_bassPatterns[1]); // Half
                    break;

                case MusicStyle.Triumphant:
                    // Strong downbeats, confident march
                    if (tension < 0.4f)
                    {
                        pool.Add(_bassPatterns[1]); // Half
                        pool.Add(_bassPatterns[7]); // Dotted
                    }
                    else
                    {
                        pool.Add(_bassPatterns[2]); // Quarters
                        pool.Add(_bassPatterns[3]); // Bouncing
                    }
                    break;

                case MusicStyle.Horror:
                    // Just root, sustained — nothing else
                    pool.Add(_bassPatterns[0]); // Whole note only
                    break;
            }

            if (pool.Count == 0)
                pool.Add(_bassPatterns[0]);

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
            if (tension > 0f && RootOnly)
                return root;

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

            // Low tension, weak beat: walk between chord tones instead of camping on root
            float rWalk = (float)_rng.NextDouble();
            if (rWalk < 0.40f) return root;
            if (rWalk < 0.65f) return fifth;
            if (rWalk < 0.85f) return third;
            return octaveUp;
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
        public MusicStyle Style = MusicStyle.Folk;

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

            // Style-specific percussion behavior
            int kickOnsets;
            bool[] snarePattern = new bool[steps];
            bool[] hihatPattern = new bool[steps];

            switch (Style)
            {
                case MusicStyle.Folk:
                    // Light, brushy — minimal kick, soft hihat
                    kickOnsets = Mathf.RoundToInt(Mathf.Lerp(2, 3, tension));
                    snarePattern[4] = true; snarePattern[12] = true;
                    if (tension < 0.4f)
                        for (int s = 0; s < steps; s += 4) hihatPattern[s] = true; // Quarters
                    else
                        for (int s = 0; s < steps; s += 2) hihatPattern[s] = true; // Eighths
                    break;

                case MusicStyle.Tense:
                    // Driving, aggressive — more kick, ghosts, sixteenths
                    kickOnsets = Mathf.RoundToInt(Mathf.Lerp(3, 4, tension));
                    snarePattern[4] = true; snarePattern[12] = true;
                    if (tension > 0.5f) snarePattern[10] = true;
                    if (tension > 0.8f) snarePattern[6] = true;
                    if (tension < 0.5f)
                        for (int s = 0; s < steps; s += 2) hihatPattern[s] = true;
                    else
                        for (int s = 0; s < steps; s++) hihatPattern[s] = true; // Sixteenths
                    break;

                case MusicStyle.Triumphant:
                    // March-like — strong downbeats, steady
                    kickOnsets = Mathf.RoundToInt(Mathf.Lerp(2, 4, tension));
                    snarePattern[4] = true; snarePattern[12] = true;
                    for (int s = 0; s < steps; s += 4) hihatPattern[s] = true;
                    if (tension > 0.5f)
                        for (int s = 0; s < steps; s += 2) hihatPattern[s] = true;
                    break;

                case MusicStyle.Sparse:
                    // Barely there — just a heartbeat kick, nothing else
                    kickOnsets = 1;
                    // No snare, no hihat
                    break;

                case MusicStyle.Horror:
                default:
                    // No percussion in horror
                    return events;
            }

            bool[] kickPattern = EuclideanRhythm(kickOnsets, steps);

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
        public float TensionTarget = 0.3f;       // Clamped by MaxTension — controls chord selection
        public float LayerTension = 0.3f;         // Unclamped — controls which layers play
        public Key CurrentKey;

        public int PadInstrumentIndex = 0;
        public int LeadInstrumentIndex = 1;
        public int BassInstrumentIndex = 2;
        public int KickInstrumentIndex = 3;
        public int SnareInstrumentIndex = 4;
        public int HiHatInstrumentIndex = 5;
        public int StringsInstrumentIndex = 6;
        public int KanteleInstrumentIndex = 7;
        public int SubDroneInstrumentIndex = 8;
        public int ShriekInstrumentIndex = 9;
        public int BanjoInstrumentIndex = 10;

        public bool EnablePad = true;
        public bool EnableMelody = true;
        public bool EnableBass = true;
        public bool BassRootOnly = false;
        public bool EnablePercussion = true;
        public bool EnableStrings = true;
        public bool EnableKantele = true;
        public bool AllowTritones = false;

        // Per-instrument styles (Melody, Bass, Rhythm have their own .Style fields
        // set from the bridge. Strings and Kantele use these since they're inline in GenerateMeasure)
        public MusicStyle CurrentMusicStyle = MusicStyle.Folk; // Global style
        public MusicStyle CurrentStringStyle = MusicStyle.Folk;
        public MusicStyle CurrentKanteleStyle = MusicStyle.Folk;

        // Per-state layer ranges (set by bridge from MusicStateConfig)
        public LayerRange PadRange = LayerRange.Always;
        public LayerRange MelodyRange = LayerRange.From(0.15f);
        public LayerRange BassRange = LayerRange.From(0.4f);
        public LayerRange PercussionRange = LayerRange.From(0.55f);
        public LayerRange StringsRange = LayerRange.From(0.3f);
        public LayerRange KanteleRange = LayerRange.Always;
        public LayerRange SubDroneRange = LayerRange.Off;
        public LayerRange ShriekRange = LayerRange.Off;
        public LayerRange BanjoRange = LayerRange.Off;

        // Scheduled events queue with absolute beat timestamps
        private List<(float absoluteBeat, NoteEvent noteEvent)> _pendingNoteOns
            = new List<(float, NoteEvent)>();
        private List<(float absoluteBeat, int midiNote, int instIndex)> _pendingNoteOffs
            = new List<(float, int, int)>();

        private float _currentBeat;
        private float _nextChordBeat;
        private float _smoothedTension;
        private float _smoothedLayerTension;
        private bool _firstUpdate = true;
        private System.Random _sparseRng = new System.Random();

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
            _smoothedLayerTension = Mathf.Lerp(_smoothedLayerTension, LayerTension, deltaTime * 2f);

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
            float t = _smoothedTension;         // Clamped by MaxTension — for chord/musical decisions
            float lt = _smoothedLayerTension;   // Unclamped — for layer entry decisions

            Chord newChord = Chords.GetNextChord(t, AllowTritones);

            // ── Layer activation ──
            // Each layer uses its LayerRange to decide if it plays at the current tension.
            // LayerRange supports both "enters at X" and "exits at X" per state per instrument.
            bool padActive = PadRange.IsActive(lt);
            bool kanteleActive = KanteleRange.IsActive(lt);
            bool melodyActive = MelodyRange.IsActive(lt);
            bool stringsActive = StringsRange.IsActive(lt);
            bool bassActive = BassRange.IsActive(lt);
            bool percActive = PercussionRange.IsActive(lt);

            // Pad (Hurdy-Gurdy drone): always present as the harmonic bed
            if (padActive)
            {
                float padDuration = BeatsPerChord + 0.15f;
                int[] notes = newChord.GetMidiNotes(3);
                foreach (int note in notes)
                {
                    var evt = new NoteEvent(note, 0.45f, padDuration, PadInstrumentIndex, 0f);
                    _pendingNoteOns.Add((measureStart, evt));
                }
            }

            // Melody (Flute): enters at tension 0.15
            if (melodyActive)
            {
                var melodyEvents = Melody.GeneratePhrase(
                    newChord, CurrentKey, t, BeatsPerChord, LeadInstrumentIndex);
                foreach (var evt in melodyEvents)
                    _pendingNoteOns.Add((measureStart + evt.BeatOffset, evt));
            }

            // Bass: enters at tension 0.4
            if (bassActive)
            {
                Bass.RootOnly = BassRootOnly;
                var bassEvents = Bass.GeneratePhrase(
                    newChord, CurrentKey, t, BeatsPerChord, BassInstrumentIndex);
                foreach (var evt in bassEvents)
                    _pendingNoteOns.Add((measureStart + evt.BeatOffset, evt));
            }

            // Percussion: enters at tension 0.55
            if (percActive)
            {
                var percEvents = Rhythm.GeneratePattern(
                    t, BeatsPerChord,
                    KickInstrumentIndex, SnareInstrumentIndex, HiHatInstrumentIndex);
                foreach (var evt in percEvents)
                    _pendingNoteOns.Add((measureStart + evt.BeatOffset, evt));
            }

            // Strings: style-aware articulation
            if (stringsActive)
            {
                bool stringsSkip = (CurrentStringStyle == MusicStyle.Sparse || CurrentStringStyle == MusicStyle.Horror)
                    && (float)_sparseRng.NextDouble() < 0.4f;

                if (!stringsSkip)
                {
                    int[] stringNotes = newChord.GetMidiNotes(4);

                    switch (CurrentStringStyle)
                    {
                        case MusicStyle.Folk:
                            // Warm sustained chords, gentle staggered entry
                            for (int i = 0; i < stringNotes.Length; i++)
                            {
                                float stagger = i * 0.05f;
                                float vel = Mathf.Lerp(0.3f, 0.55f, t);
                                var evt = new NoteEvent(stringNotes[i], vel, BeatsPerChord - 0.1f,
                                    StringsInstrumentIndex, stagger);
                                _pendingNoteOns.Add((measureStart + stagger, evt));
                            }
                            break;

                        case MusicStyle.Triumphant:
                            // Bold rhythmic chords on beats 1 and 3
                            {
                                float stringVel = 0.6f;
                                foreach (int note in stringNotes)
                                {
                                    _pendingNoteOns.Add((measureStart,
                                        new NoteEvent(note, stringVel, 1.8f, StringsInstrumentIndex, 0f)));
                                }
                                if (BeatsPerChord >= 4)
                                {
                                    foreach (int note in stringNotes)
                                    {
                                        _pendingNoteOns.Add((measureStart + 2f,
                                            new NoteEvent(note, stringVel * 0.85f, 1.8f, StringsInstrumentIndex, 2f)));
                                    }
                                }
                            }
                            break;

                        case MusicStyle.Tense:
                            // Rhythmic pulsing on every beat — driving
                            {
                                float stringVel = Mathf.Lerp(0.45f, 0.65f, t);
                                float hitSpacing = 1f;
                                for (int hit = 0; hit < BeatsPerChord; hit++)
                                {
                                    float beatPos = hit * hitSpacing;
                                    float hitDur = hitSpacing + 0.1f;
                                    float hitVel = (hit == 0) ? stringVel : stringVel * 0.85f;
                                    foreach (int note in stringNotes)
                                    {
                                        _pendingNoteOns.Add((measureStart + beatPos,
                                            new NoteEvent(note, hitVel, hitDur, StringsInstrumentIndex, beatPos)));
                                    }
                                }
                            }
                            break;

                        case MusicStyle.Sparse:
                            // Soft sustained chord, widely staggered — ghostly
                            for (int i = 0; i < stringNotes.Length; i++)
                            {
                                float stagger = i * 0.15f;
                                var evt = new NoteEvent(stringNotes[i], 0.22f, BeatsPerChord + 0.5f,
                                    StringsInstrumentIndex, stagger);
                                _pendingNoteOns.Add((measureStart + stagger, evt));
                            }
                            break;

                        case MusicStyle.Horror:
                            // Single dissonant note — semitone up from root = maximum clash
                            {
                                int note = stringNotes[0] + 1;
                                _pendingNoteOns.Add((measureStart,
                                    new NoteEvent(note, 0.18f, BeatsPerChord + 1f, StringsInstrumentIndex, 0f)));
                            }
                            break;
                    }
                } // end stringsSkip check
            }

            // Kantele: style-aware folk plucking
            if (kanteleActive)
            {
                bool kanteleSkip = (CurrentKanteleStyle == MusicStyle.Sparse || CurrentKanteleStyle == MusicStyle.Horror)
                    && (float)_sparseRng.NextDouble() < 0.5f;

                if (!kanteleSkip)
                {
                    int[] chordPCs = newChord.GetPitchClasses();
                    int kanteleOctave = 4;

                    var kantelePitches = new List<int>();
                    foreach (int pc in chordPCs)
                        kantelePitches.Add((kanteleOctave + 1) * 12 + pc);
                    kantelePitches.Add((kanteleOctave + 2) * 12 + chordPCs[0]);

                    switch (CurrentKanteleStyle)
                    {
                        case MusicStyle.Folk:
                            // Gentle flowing arpeggios — campfire strumming
                            if (t < 0.3f)
                            {
                                float[] pluckTimes = { 0f, 1.5f, 3f };
                                for (int p = 0; p < pluckTimes.Length && p < kantelePitches.Count; p++)
                                {
                                    if (pluckTimes[p] >= BeatsPerChord) break;
                                    int note = kantelePitches[p % kantelePitches.Count];
                                    _pendingNoteOns.Add((measureStart + pluckTimes[p],
                                        new NoteEvent(note, 0.4f, 1.5f, KanteleInstrumentIndex, pluckTimes[p])));
                                }
                            }
                            else
                            {
                                // Flowing eighth-note arpeggios
                                float spacing = 0.5f;
                                for (int p = 0; p < kantelePitches.Count; p++)
                                {
                                    float beatPos = p * spacing;
                                    if (beatPos >= BeatsPerChord) break;
                                    _pendingNoteOns.Add((measureStart + beatPos,
                                        new NoteEvent(kantelePitches[p], 0.38f, 1.0f, KanteleInstrumentIndex, beatPos)));
                                }
                                for (int p = 0; p < kantelePitches.Count; p++)
                                {
                                    float beatPos = 2f + p * spacing;
                                    if (beatPos >= BeatsPerChord) break;
                                    _pendingNoteOns.Add((measureStart + beatPos,
                                        new NoteEvent(kantelePitches[p], 0.33f, 1.0f, KanteleInstrumentIndex, beatPos)));
                                }
                            }
                            break;

                        case MusicStyle.Triumphant:
                            // Bold downbeat strums — confident, march-like
                            {
                                foreach (int note in kantelePitches)
                                {
                                    _pendingNoteOns.Add((measureStart,
                                        new NoteEvent(note, 0.5f, 1.0f, KanteleInstrumentIndex, 0f)));
                                }
                                if (BeatsPerChord >= 4)
                                {
                                    foreach (int note in kantelePitches)
                                    {
                                        _pendingNoteOns.Add((measureStart + 2f,
                                            new NoteEvent(note, 0.4f, 1.0f, KanteleInstrumentIndex, 2f)));
                                    }
                                }
                            }
                            break;

                        case MusicStyle.Tense:
                            // Rapid tremolo on root and fifth — urgent, nervous
                            {
                                int rootNote = kantelePitches[0];
                                int fifthNote = kantelePitches.Count > 2 ? kantelePitches[2] : rootNote;
                                float spacing = 0.25f;
                                for (int p = 0; p < BeatsPerChord / spacing; p++)
                                {
                                    float beatPos = p * spacing;
                                    if (beatPos >= BeatsPerChord) break;
                                    int note = (p % 2 == 0) ? rootNote : fifthNote;
                                    float vel = 0.3f + (p % 4 == 0 ? 0.1f : 0f);
                                    _pendingNoteOns.Add((measureStart + beatPos,
                                        new NoteEvent(note, vel, 0.3f, KanteleInstrumentIndex, beatPos)));
                                }
                            }
                            break;

                        case MusicStyle.Sparse:
                            // Single lonely pluck, then silence
                            {
                                int note = kantelePitches[0];
                                _pendingNoteOns.Add((measureStart,
                                    new NoteEvent(note, 0.3f, 2.0f, KanteleInstrumentIndex, 0f)));
                            }
                            break;

                        case MusicStyle.Horror:
                            // Dissonant single pluck — wrong note, unsettling
                            {
                                int note = kantelePitches[0] + 1; // Semitone up = clash
                                _pendingNoteOns.Add((measureStart,
                                    new NoteEvent(note, 0.2f, 3.0f, KanteleInstrumentIndex, 0f)));
                            }
                            break;
                    }
                } // end kanteleSkip check
            }

            // Sub Drone: extremely low sustained root note — the rumble of dread
            // Only active in Horror. Just holds one note per measure, very low octave.
            bool subDroneActive = SubDroneRange.IsActive(lt);
            if (subDroneActive)
            {
                int droneNote = TonalPitchSpace.ToMidi(newChord.Root, 1); // Octave 1 — deep sub
                float droneDuration = BeatsPerChord + 0.3f; // Overlap into next chord
                _pendingNoteOns.Add((measureStart,
                    new NoteEvent(droneNote, 0.5f, droneDuration, SubDroneInstrumentIndex, 0f)));
            }

            // Shriek String: high-pitched horror string that crawls by semitones and tritones
            // Sparse — skips many measures. When it plays, it picks a note in the shriek register
            // (octave 6) and moves by semitone or tritone from the chord root. Terrifying.
            bool shriekActive = ShriekRange.IsActive(lt);
            if (shriekActive)
            {
                // 55% of measures are silent — the shriek is rare, which makes it worse
                if ((float)_sparseRng.NextDouble() > 0.55f)
                {
                    int[] intervals = { 0, 1, -1, 6, -6, 11, -11, 1, -1 }; // Semitones and tritones
                    int baseNote = TonalPitchSpace.ToMidi(newChord.Root, 6); // Octave 6 — high shriek
                    int offset = intervals[_sparseRng.Next(intervals.Length)];
                    int shriekNote = Mathf.Clamp(baseNote + offset, 84, 96); // C6 to C7

                    // Long sustain — the note hangs and warps with the vibrato
                    float shriekDuration = BeatsPerChord + 1f;
                    float shriekVel = 0.15f + (float)_sparseRng.NextDouble() * 0.15f; // Quiet but piercing
                    _pendingNoteOns.Add((measureStart,
                        new NoteEvent(shriekNote, shriekVel, shriekDuration, ShriekInstrumentIndex, 0f)));
                }
            }

            // Banjo: rolling fingerpick pattern — cozy campfire feel
            // Classic 3-finger roll: thumb plays root, index plays middle, middle plays high
            // Creates that warm, rolling sound that says "everything is going to be okay"
            bool banjoActive = BanjoRange.IsActive(lt);
            if (banjoActive)
            {
                int[] chordPCs = newChord.GetPitchClasses();
                int banjoOctave = 4;

                // Build chord tones for fingerpicking
                int root = (banjoOctave + 1) * 12 + chordPCs[0];
                int mid = (banjoOctave + 1) * 12 + (chordPCs.Length > 1 ? chordPCs[1] : chordPCs[0]);
                int high = (banjoOctave + 1) * 12 + (chordPCs.Length > 2 ? chordPCs[2] : chordPCs[0]);
                int octUp = root + 12;

                // Rolling fingerpick pattern: T-I-M-T-I-M-T-M (classic bluegrass/folk)
                // T=thumb(root), I=index(mid), M=middle(high)
                // Spacing at eighth notes (0.5 beats) for gentle rolling feel
                float[][] rollPatterns = new float[][]
                {
                    // Pattern 1: forward roll — T I M T I M T M
                    new float[] { 0f, 0.5f, 1f, 1.5f, 2f, 2.5f, 3f, 3.5f },
                    // Pattern 2: alternating — T M I M T M I M
                    new float[] { 0f, 0.5f, 1f, 1.5f, 2f, 2.5f, 3f, 3.5f },
                    // Pattern 3: sparse — T . M . T . I .
                    new float[] { 0f, 1f, 2f, 3f },
                };

                int patternChoice = _sparseRng.Next(3);
                float[] pattern = rollPatterns[patternChoice];

                for (int p = 0; p < pattern.Length; p++)
                {
                    if (pattern[p] >= BeatsPerChord) break;

                    int note;
                    if (patternChoice == 0)
                    {
                        // Forward roll: root, mid, high, root, mid, high, root, high
                        int[] roll = { root, mid, high, root, mid, high, root, high };
                        note = roll[p % roll.Length];
                    }
                    else if (patternChoice == 1)
                    {
                        // Alternating: root, high, mid, high, root, high, mid, high
                        int[] roll = { root, high, mid, high, root, high, mid, high };
                        note = roll[p % roll.Length];
                    }
                    else
                    {
                        // Sparse: root, high, root, mid
                        int[] roll = { root, high, root, mid };
                        note = roll[p % roll.Length];
                    }

                    // Thumb notes (root) slightly louder, fingers softer
                    float vel = (note == root) ? 0.45f : 0.35f;
                    // Slight humanization
                    vel *= 0.9f + (float)_sparseRng.NextDouble() * 0.2f;

                    _pendingNoteOns.Add((measureStart + pattern[p],
                        new NoteEvent(note, vel, 0.4f, BanjoInstrumentIndex, pattern[p])));
                }
            }
        }
        public void ChangeKey(Key newKey)
        {
            CurrentKey = newKey;
            Chords.Modulate(newKey);
        }

        /// <summary>
        /// Sets the music style on ALL generators at once.
        /// This controls rhythm patterns, articulation, and density for every instrument.
        /// </summary>
        public void SetMusicStyle(MusicStyle style)
        {
            CurrentMusicStyle = style;
            Melody.Style = style;
            Bass.Style = style;
            Rhythm.Style = style;
            CurrentStringStyle = style;
            CurrentKanteleStyle = style;
        }
    }
}