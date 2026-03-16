using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ProceduralMusic.Core
{
    /// <summary>
    /// Represents a pitch class (0-11, where 0 = C).
    /// </summary>
    public enum PitchClass
    {
        C = 0, Cs = 1, D = 2, Ds = 3, E = 4, F = 5,
        Fs = 6, G = 7, Gs = 8, A = 9, As = 10, B = 11
    }

    /// <summary>
    /// Chord quality types supported by the system.
    /// </summary>
    public enum ChordQuality
    {
        Major,
        Minor,
        Diminished,
        Augmented,
        Dominant7,
        Major7,
        Minor7
    }

    /// <summary>
    /// Scale/key mode.
    /// </summary>
    public enum MusicalMode
    {
        Major,
        NaturalMinor,
        HarmonicMinor,
        Dorian,
        Mixolydian,
        Aeolian,
        Phrygian,       // Flat 2nd — dark, Spanish, spooky
        Locrian         // Flat 2nd and flat 5th — very unstable, eerie
    }

    /// <summary>
    /// A chord defined by root pitch class and quality.
    /// </summary>
    [Serializable]
    public struct Chord
    {
        public PitchClass Root;
        public ChordQuality Quality;

        public Chord(PitchClass root, ChordQuality quality)
        {
            Root = root;
            Quality = quality;
        }

        /// <summary>
        /// Returns the pitch classes in this chord.
        /// </summary>
        public int[] GetPitchClasses()
        {
            int r = (int)Root;
            switch (Quality)
            {
                case ChordQuality.Major:       return new[] { r, (r + 4) % 12, (r + 7) % 12 };
                case ChordQuality.Minor:       return new[] { r, (r + 3) % 12, (r + 7) % 12 };
                case ChordQuality.Diminished:  return new[] { r, (r + 3) % 12, (r + 6) % 12 };
                case ChordQuality.Augmented:   return new[] { r, (r + 4) % 12, (r + 8) % 12 };
                case ChordQuality.Dominant7:   return new[] { r, (r + 4) % 12, (r + 7) % 12, (r + 10) % 12 };
                case ChordQuality.Major7:      return new[] { r, (r + 4) % 12, (r + 7) % 12, (r + 11) % 12 };
                case ChordQuality.Minor7:      return new[] { r, (r + 3) % 12, (r + 7) % 12, (r + 10) % 12 };
                default:                       return new[] { r, (r + 4) % 12, (r + 7) % 12 };
            }
        }

        /// <summary>
        /// Returns MIDI note numbers for this chord in a given octave with voicing.
        /// </summary>
        public int[] GetMidiNotes(int baseOctave = 4)
        {
            int baseMidi = (baseOctave + 1) * 12;
            int r = (int)Root;
            var intervals = GetIntervals();
            var notes = new int[intervals.Length];
            for (int i = 0; i < intervals.Length; i++)
            {
                notes[i] = baseMidi + r + intervals[i];
            }
            return notes;
        }

        private int[] GetIntervals()
        {
            switch (Quality)
            {
                case ChordQuality.Major:       return new[] { 0, 4, 7 };
                case ChordQuality.Minor:       return new[] { 0, 3, 7 };
                case ChordQuality.Diminished:  return new[] { 0, 3, 6 };
                case ChordQuality.Augmented:   return new[] { 0, 4, 8 };
                case ChordQuality.Dominant7:   return new[] { 0, 4, 7, 10 };
                case ChordQuality.Major7:      return new[] { 0, 4, 7, 11 };
                case ChordQuality.Minor7:      return new[] { 0, 3, 7, 10 };
                default:                       return new[] { 0, 4, 7 };
            }
        }

        public override string ToString() => $"{Root}{Quality}";
    }

    /// <summary>
    /// Represents a musical key (root + mode) with its diatonic chords.
    /// </summary>
    [Serializable]
    public struct Key
    {
        public PitchClass Root;
        public MusicalMode Mode;

        public Key(PitchClass root, MusicalMode mode)
        {
            Root = root;
            Mode = mode;
        }

        /// <summary>
        /// Returns the scale degrees (semitone offsets) for this key's mode.
        /// </summary>
        public int[] GetScaleDegrees()
        {
            switch (Mode)
            {
                case MusicalMode.Major:         return new[] { 0, 2, 4, 5, 7, 9, 11 };
                case MusicalMode.NaturalMinor:  return new[] { 0, 2, 3, 5, 7, 8, 10 };
                case MusicalMode.HarmonicMinor: return new[] { 0, 2, 3, 5, 7, 8, 11 };
                case MusicalMode.Dorian:        return new[] { 0, 2, 3, 5, 7, 9, 10 };
                case MusicalMode.Mixolydian:    return new[] { 0, 2, 4, 5, 7, 9, 10 };
                case MusicalMode.Aeolian:       return new[] { 0, 2, 3, 5, 7, 8, 10 };
                case MusicalMode.Phrygian:      return new[] { 0, 1, 3, 5, 7, 8, 10 }; // Flat 2
                case MusicalMode.Locrian:       return new[] { 0, 1, 3, 5, 6, 8, 10 }; // Flat 2, flat 5
                default:                        return new[] { 0, 2, 4, 5, 7, 9, 11 };
            }
        }

        /// <summary>
        /// Returns the pitch classes in this key's scale.
        /// </summary>
        public int[] GetScalePitchClasses()
        {
            int r = (int)Root;
            return GetScaleDegrees().Select(d => (r + d) % 12).ToArray();
        }

        /// <summary>
        /// Returns the diatonic triads for this key.
        /// </summary>
        public Chord[] GetDiatonicChords()
        {
            var degrees = GetScaleDegrees();
            int r = (int)Root;
            var chords = new Chord[7];

            // Major scale diatonic qualities: I ii iii IV V vi vii°
            ChordQuality[] majorQualities = {
                ChordQuality.Major, ChordQuality.Minor, ChordQuality.Minor,
                ChordQuality.Major, ChordQuality.Major, ChordQuality.Minor,
                ChordQuality.Diminished
            };

            // Minor scale diatonic qualities: i ii° III iv v VI VII
            ChordQuality[] minorQualities = {
                ChordQuality.Minor, ChordQuality.Diminished, ChordQuality.Major,
                ChordQuality.Minor, ChordQuality.Minor, ChordQuality.Major,
                ChordQuality.Major
            };

            // Phrygian diatonic qualities: i bII bIII iv v° bVI bvii
            ChordQuality[] phrygianQualities = {
                ChordQuality.Minor, ChordQuality.Major, ChordQuality.Major,
                ChordQuality.Minor, ChordQuality.Diminished, ChordQuality.Major,
                ChordQuality.Minor
            };

            // Locrian diatonic qualities: i° bII biii iv bV bVI bvii
            ChordQuality[] locrianQualities = {
                ChordQuality.Diminished, ChordQuality.Major, ChordQuality.Minor,
                ChordQuality.Minor, ChordQuality.Major, ChordQuality.Major,
                ChordQuality.Minor
            };

            ChordQuality[] qualities;
            if (Mode == MusicalMode.Major || Mode == MusicalMode.Mixolydian)
                qualities = majorQualities;
            else if (Mode == MusicalMode.Phrygian)
                qualities = phrygianQualities;
            else if (Mode == MusicalMode.Locrian)
                qualities = locrianQualities;
            else
                qualities = minorQualities;

            for (int i = 0; i < 7; i++)
            {
                chords[i] = new Chord((PitchClass)((r + degrees[i]) % 12), qualities[i]);
            }

            return chords;
        }

        public override string ToString() => $"{Root} {Mode}";
    }

    /// <summary>
    /// Implements Lerdahl's Tonal Pitch Space distance and tension calculations.
    /// 
    /// The model defines four hierarchical levels:
    ///   Level a (chromatic):  all 12 pitch classes
    ///   Level b (diatonic):   the 7 scale tones
    ///   Level c (triadic):    the 3 chord tones of the current chord
    ///   Level d (root):       the root of the current chord
    ///
    /// Distance between chords/keys is computed by counting pitch classes
    /// that must be added or removed when moving between contexts.
    /// </summary>
    public static class TonalPitchSpace
    {
        // ─────────────────────────────────────────────
        //  DISTANCE CALCULATIONS
        // ─────────────────────────────────────────────

        /// <summary>
        /// Computes the chord distance δ(x, y) within a single key,
        /// following Lerdahl's basic pitch space model.
        /// 
        /// Distance = (root movement on circle of fifths)
        ///          + (number of non-common tones at diatonic level)
        ///          + (number of non-common tones at triadic level)
        /// </summary>
        public static float ChordDistance(Chord from, Chord to, Key key)
        {
            // Component 1: Root distance on circle of fifths
            int fifthsDist = CircleOfFifthsDistance((int)from.Root, (int)to.Root);

            // Component 2: Non-common tones at triadic level
            var fromPCs = new HashSet<int>(from.GetPitchClasses());
            var toPCs = new HashSet<int>(to.GetPitchClasses());
            int triadicNonCommon = toPCs.Count(pc => !fromPCs.Contains(pc));

            // Component 3: Non-common tones at diatonic level (within key context)
            var scalePCs = new HashSet<int>(key.GetScalePitchClasses());
            int diatonicComponent = 0;
            foreach (int pc in toPCs)
            {
                if (!fromPCs.Contains(pc) && scalePCs.Contains(pc))
                    diatonicComponent++;
            }

            return fifthsDist + triadicNonCommon + diatonicComponent;
        }

        /// <summary>
        /// Computes inter-key distance: how far apart two keys are.
        /// Based on the number of non-common pitch classes at the diatonic level
        /// plus the root distance on the circle of fifths.
        /// </summary>
        public static float KeyDistance(Key from, Key to)
        {
            int fifthsDist = CircleOfFifthsDistance((int)from.Root, (int)to.Root);

            var fromScale = new HashSet<int>(from.GetScalePitchClasses());
            var toScale = new HashSet<int>(to.GetScalePitchClasses());
            int nonCommon = toScale.Count(pc => !fromScale.Contains(pc));

            return fifthsDist + nonCommon;
        }

        /// <summary>
        /// Combined distance for a chord change that may also involve a key change.
        /// δ_total = δ_key(k1, k2) + δ_chord(c1, c2, k2)
        /// </summary>
        public static float TotalDistance(Chord fromChord, Key fromKey, Chord toChord, Key toKey)
        {
            return KeyDistance(fromKey, toKey) + ChordDistance(fromChord, toChord, toKey);
        }

        // ─────────────────────────────────────────────
        //  TENSION MODEL
        // ─────────────────────────────────────────────

        /// <summary>
        /// Calculates the tension of a chord relative to a tonal context.
        /// Higher values = more tension (desire to resolve).
        /// 
        /// Tension components:
        ///   1. Hierarchical tension: distance from tonic chord in the key
        ///   2. Surface dissonance: inherent dissonance of the chord quality
        ///   3. Attraction: melodic tendency of chord tones toward stable pitches
        /// </summary>
        public static float GetTension(Chord chord, Key key)
        {
            // Tonic chord of the key
            Chord tonic = new Chord(key.Root,
                (key.Mode == MusicalMode.Major || key.Mode == MusicalMode.Mixolydian)
                    ? ChordQuality.Major : ChordQuality.Minor);

            // Hierarchical tension: distance from tonic
            float hierarchical = ChordDistance(tonic, chord, key);

            // Surface dissonance component
            float dissonance = GetChordDissonance(chord);

            // Weighted sum
            return (hierarchical * 1.0f) + (dissonance * 0.5f);
        }

        /// <summary>
        /// Computes melodic attraction of a pitch to the nearest stable tone.
        /// Based on Lerdahl's attraction formula:
        ///   α(p1→p2) = (stability(p2) - stability(p1)) / distance(p1, p2)^2
        ///
        /// Returns a value where higher = stronger pull toward stability.
        /// </summary>
        public static float GetAttraction(int pitchClass, Chord currentChord, Key key)
        {
            int[] scalePCs = key.GetScalePitchClasses();
            int[] chordPCs = currentChord.GetPitchClasses();

            float stability = GetPitchStability(pitchClass, chordPCs, scalePCs, (int)key.Root);

            // If already very stable, low attraction (it's already "home")
            if (stability >= 3.5f) return 0.1f;

            // Find the nearest more-stable pitch and compute attraction
            float maxAttraction = 0f;
            for (int offset = 1; offset <= 6; offset++)
            {
                foreach (int dir in new[] { 1, -1 })
                {
                    int neighbor = ((pitchClass + dir * offset) % 12 + 12) % 12;
                    float neighborStability = GetPitchStability(neighbor, chordPCs, scalePCs, (int)key.Root);

                    if (neighborStability > stability)
                    {
                        float distance = offset; // semitone distance
                        float attraction = (neighborStability - stability) / (distance * distance);
                        maxAttraction = Mathf.Max(maxAttraction, attraction);
                    }
                }
            }

            return maxAttraction;
        }

        /// <summary>
        /// Returns pitch stability (0-4 scale) based on position in the TPS hierarchy.
        ///   4 = root of key/chord
        ///   3 = chord tone (non-root)
        ///   2 = diatonic scale tone (non-chord)
        ///   1 = chromatic (non-diatonic)
        /// </summary>
        public static float GetPitchStability(int pitchClass, int[] chordTones, int[] scaleTones, int keyRoot)
        {
            if (pitchClass == keyRoot && chordTones.Contains(pitchClass))
                return 4f;
            if (chordTones.Contains(pitchClass))
                return 3f;
            if (scaleTones.Contains(pitchClass))
                return 2f;
            return 1f;
        }

        // ─────────────────────────────────────────────
        //  CHORD SUGGESTION
        // ─────────────────────────────────────────────

        /// <summary>
        /// Suggests the next chord given a desired tension level (0 = very relaxed, 1 = very tense).
        /// Returns a list of candidate chords sorted by how well they match the target tension.
        /// </summary>
        public static List<Chord> SuggestNextChords(Chord current, Key key, float targetTension, int maxResults = 4)
        {
            var diatonic = key.GetDiatonicChords();
            float maxTensionInKey = 0f;

            // Calculate tension for all diatonic chords
            var chordTensions = new List<(Chord chord, float tension, float distance)>();
            foreach (var chord in diatonic)
            {
                float tension = GetTension(chord, key);
                float distance = ChordDistance(current, chord, key);
                chordTensions.Add((chord, tension, distance));
                maxTensionInKey = Mathf.Max(maxTensionInKey, tension);
            }

            // Also consider secondary dominants for higher tension
            if (targetTension > 0.6f)
            {
                foreach (var diaChord in diatonic)
                {
                    // Secondary dominant: V/x
                    var secDom = new Chord(
                        (PitchClass)(((int)diaChord.Root + 7) % 12),
                        ChordQuality.Dominant7
                    );
                    float tension = GetTension(secDom, key) + 2f; // Bonus tension for chromatic
                    float distance = ChordDistance(current, secDom, key);
                    chordTensions.Add((secDom, tension, distance));
                    maxTensionInKey = Mathf.Max(maxTensionInKey, tension);
                }
            }

            if (maxTensionInKey < 0.01f) maxTensionInKey = 1f;

            // Score each chord by how close its normalized tension matches the target
            float targetTensionScaled = targetTension * maxTensionInKey;

            var scored = chordTensions
                .Select(ct => new
                {
                    ct.chord,
                    score = -Mathf.Abs(ct.tension - targetTensionScaled) - ct.distance * 0.3f
                })
                .OrderByDescending(x => x.score)
                .Take(maxResults)
                .Select(x => x.chord)
                .ToList();

            return scored;
        }

        // ─────────────────────────────────────────────
        //  UTILITIES
        // ─────────────────────────────────────────────

        /// <summary>
        /// Shortest distance on the circle of fifths between two pitch classes.
        /// </summary>
        public static int CircleOfFifthsDistance(int pc1, int pc2)
        {
            // Position on circle of fifths: multiply by 7 mod 12
            int pos1 = (pc1 * 7) % 12;
            int pos2 = (pc2 * 7) % 12;
            int diff = Mathf.Abs(pos1 - pos2);
            return Mathf.Min(diff, 12 - diff);
        }

        /// <summary>
        /// Inherent dissonance rating for a chord quality (0-3 scale).
        /// </summary>
        public static float GetChordDissonance(Chord chord)
        {
            switch (chord.Quality)
            {
                case ChordQuality.Major:      return 0f;
                case ChordQuality.Minor:      return 0.2f;
                case ChordQuality.Major7:     return 0.8f;
                case ChordQuality.Minor7:     return 0.9f;
                case ChordQuality.Dominant7:  return 1.2f;
                case ChordQuality.Augmented:  return 1.8f;
                case ChordQuality.Diminished: return 2.0f;
                default:                      return 0f;
            }
        }

        /// <summary>
        /// Converts MIDI note number to frequency in Hz (A4 = 440Hz).
        /// </summary>
        public static float MidiToFrequency(int midiNote)
        {
            return 440f * Mathf.Pow(2f, (midiNote - 69) / 12f);
        }

        /// <summary>
        /// Converts pitch class + octave to MIDI note number.
        /// </summary>
        public static int ToMidi(PitchClass pc, int octave)
        {
            return (octave + 1) * 12 + (int)pc;
        }
    }
}
