using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProceduralMusic.Synthesis
{
    /// <summary>
    /// Instrument preset defining the sound characteristics of a synth voice.
    /// </summary>
    [Serializable]
    public class InstrumentPreset
    {
        public string Name;
        public SynthVoice.SynthType SynthType;
        public int MaxPolyphony = 4;

        // ADSR
        public float Attack = 0.01f;
        public float Decay = 0.1f;
        public float Sustain = 0.7f;
        public float Release = 0.3f;

        // Subtractive
        public Waveform OscWaveform = Waveform.Saw;
        public float FilterCutoff = 2000f;
        public float FilterEnvAmount = 4000f;

        // FM
        public float FMRatio = 2f;
        public float FMIndex = 1f;
        public float FMEnvAmount = 0.5f;

        // Portamento / Vibrato (for StringEnsemble and LegatoLead)
        public float PortamentoTime = 0.08f;
        public float VibratoRate = 5.5f;
        public float VibratoDepth = 0.004f;

        // Mixer
        public float Volume = 0.5f;
        public float Pan = 0f;

        /// <summary>
        /// Drone pad: dark, grinding hurdy-gurdy texture.
        /// Square wave strings + constant wheel buzz + pitch wobble.
        /// </summary>
        public static InstrumentPreset Pad => new InstrumentPreset
        {
            Name = "HurdyGurdy",
            SynthType = SynthVoice.SynthType.HurdyGurdy,
            MaxPolyphony = 4,
            Attack = 0.8f, Decay = 0.3f, Sustain = 0.8f, Release = 1.5f,
            FilterCutoff = 1800f, FilterEnvAmount = 600f,
            Volume = 0.22f
        };

        public static InstrumentPreset Lead => new InstrumentPreset
        {
            Name = "Lead",
            SynthType = SynthVoice.SynthType.FM,
            MaxPolyphony = 2,
            Attack = 0.01f, Decay = 0.2f, Sustain = 0.6f, Release = 0.2f,
            FMRatio = 2f, FMIndex = 1.5f, FMEnvAmount = 0.7f,
            Volume = 0.35f
        };

        public static InstrumentPreset Bass => new InstrumentPreset
        {
            Name = "Bass",
            SynthType = SynthVoice.SynthType.Subtractive,
            MaxPolyphony = 1,
            Attack = 0.003f, Decay = 0.2f, Sustain = 0.75f, Release = 0.15f,
            OscWaveform = Waveform.Saw, FilterCutoff = 400f, FilterEnvAmount = 800f,
            Volume = 0.6f
        };

        public static InstrumentPreset Kick => new InstrumentPreset
        {
            Name = "LogDrum",
            SynthType = SynthVoice.SynthType.Percussion,
            MaxPolyphony = 1,
            Attack = 0.001f, Decay = 0.45f, Sustain = 0f, Release = 0.2f,
            Volume = 0.85f
        };

        public static InstrumentPreset Snare => new InstrumentPreset
        {
            Name = "FrameDrum",
            SynthType = SynthVoice.SynthType.Percussion,
            MaxPolyphony = 1,
            Attack = 0.001f, Decay = 0.22f, Sustain = 0f, Release = 0.12f,
            Volume = 0.45f
        };

        public static InstrumentPreset HiHat => new InstrumentPreset
        {
            Name = "Brush",
            SynthType = SynthVoice.SynthType.Percussion,
            MaxPolyphony = 1,
            Attack = 0.002f, Decay = 0.06f, Sustain = 0f, Release = 0.04f,
            Volume = 0.18f  // Quieter — subtle texture
        };

        /// <summary>
        /// String ensemble: warm, rich, orchestral strings.
        /// 4 detuned saw oscillators + low-pass filter + vibrato.
        /// Slow attack for bowing feel, long release for sustain.
        /// </summary>
        public static InstrumentPreset Strings => new InstrumentPreset
        {
            Name = "Strings",
            SynthType = SynthVoice.SynthType.StringEnsemble,
            MaxPolyphony = 8,
            Attack = 0.6f, Decay = 0.2f, Sustain = 0.85f, Release = 1.2f,
            FilterCutoff = 3500f, FilterEnvAmount = 2000f,
            PortamentoTime = 0f, VibratoRate = 5.2f, VibratoDepth = 0.003f,
            Volume = 0.3f
        };

        /// <summary>
        /// Flute-like legato lead: breathy, pure, singing melody voice.
        /// Portamento glides between notes; envelope does NOT retrigger on legato.
        /// Strong fundamental + weak odd harmonics + breath noise for airiness.
        /// </summary>
        public static InstrumentPreset LegatoMelody => new InstrumentPreset
        {
            Name = "Flute",
            SynthType = SynthVoice.SynthType.LegatoLead,
            MaxPolyphony = 1,  // Monophonic for true legato
            Attack = 0.08f, Decay = 0.15f, Sustain = 0.8f, Release = 0.35f,
            FilterCutoff = 4500f, FilterEnvAmount = 2000f,
            PortamentoTime = 0.06f, VibratoRate = 5.0f, VibratoDepth = 0.005f,
            Volume = 0.35f
        };

        /// <summary>
        /// Staccato strings: shorter, plucked string-like articulation.
        /// Good for rhythmic string parts.
        /// </summary>
        public static InstrumentPreset StringsStaccato => new InstrumentPreset
        {
            Name = "StringsStaccato",
            SynthType = SynthVoice.SynthType.StringEnsemble,
            MaxPolyphony = 6,
            Attack = 0.02f, Decay = 0.3f, Sustain = 0.3f, Release = 0.2f,
            FilterCutoff = 4000f, FilterEnvAmount = 3000f,
            PortamentoTime = 0f, VibratoRate = 6f, VibratoDepth = 0.002f,
            Volume = 0.25f
        };

        /// <summary>
        /// Cello: deeper solo string sound for bass or countermelody lines.
        /// </summary>
        public static InstrumentPreset Cello => new InstrumentPreset
        {
            Name = "Cello",
            SynthType = SynthVoice.SynthType.StringEnsemble,
            MaxPolyphony = 2,
            Attack = 0.3f, Decay = 0.15f, Sustain = 0.8f, Release = 0.8f,
            FilterCutoff = 2000f, FilterEnvAmount = 1500f,
            PortamentoTime = 0.05f, VibratoRate = 5f, VibratoDepth = 0.004f,
            Volume = 0.35f
        };

        /// <summary>
        /// Kantele / plucked zither: bright plucked string that rings out.
        /// Uses Karplus-Strong synthesis. Perfect for folk arpeggios.
        /// Think Over the Garden Wall guitar or Finnish kantele.
        /// </summary>
        public static InstrumentPreset Kantele => new InstrumentPreset
        {
            Name = "Kantele",
            SynthType = SynthVoice.SynthType.PluckedString,
            MaxPolyphony = 8,
            Attack = 0.001f, Decay = 1.5f, Sustain = 0f, Release = 0.5f,
            FilterCutoff = 3500f, FilterEnvAmount = 0f,
            Volume = 0.7f
        };

        /// <summary>
        /// Darker plucked string — gut-string guitar feel.
        /// Less bright, longer sustain, warmer.
        /// </summary>
        public static InstrumentPreset GutGuitar => new InstrumentPreset
        {
            Name = "GutGuitar",
            SynthType = SynthVoice.SynthType.PluckedString,
            MaxPolyphony = 6,
            Attack = 0.001f, Decay = 2.0f, Sustain = 0f, Release = 0.8f,
            FilterCutoff = 2000f, FilterEnvAmount = 0f,
            Volume = 0.35f
        };
    }

    /// <summary>
    /// Manages a pool of synth voices for a single instrument.
    /// Handles voice allocation, stealing, and polyphony limits.
    /// </summary>
    public class VoiceManager
    {
        public InstrumentPreset Preset { get; private set; }

        private SynthVoice[] _voices;
        private float _sampleRate;

        /// <summary>Number of currently sounding voices in this instrument.</summary>
        public int ActiveVoiceCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _voices.Length; i++)
                    if (_voices[i].IsActive) count++;
                return count;
            }
        }

        /// <summary>Maximum polyphony for this instrument.</summary>
        public int MaxVoices => _voices.Length;

        public VoiceManager(InstrumentPreset preset, float sampleRate = 48000f)
        {
            Preset = preset;
            _sampleRate = sampleRate;
            _voices = new SynthVoice[preset.MaxPolyphony];

            for (int i = 0; i < preset.MaxPolyphony; i++)
            {
                _voices[i] = new SynthVoice(sampleRate);
                _voices[i].Configure(
                    preset.SynthType,
                    preset.Attack, preset.Decay, preset.Sustain, preset.Release,
                    preset.OscWaveform, preset.FilterCutoff, preset.FilterEnvAmount,
                    preset.FMRatio, preset.FMIndex, preset.FMEnvAmount,
                    preset.PortamentoTime, preset.VibratoRate, preset.VibratoDepth
                );
            }
        }

        /// <summary>
        /// Trigger a note. Returns the voice index used, or -1 if no voice available.
        /// Uses voice stealing (kills oldest active voice) when all voices are busy.
        /// </summary>
        public int NoteOn(int midiNote, float velocity = 1f)
        {
            // First, check if this note is already playing (retrigger)
            for (int i = 0; i < _voices.Length; i++)
            {
                if (_voices[i].CurrentNote == midiNote && _voices[i].IsActive)
                {
                    _voices[i].Kill();
                    _voices[i].NoteOn(midiNote, velocity * Preset.Volume);
                    _voices[i].Pan = Preset.Pan;
                    return i;
                }
            }

            // Find a free voice
            for (int i = 0; i < _voices.Length; i++)
            {
                if (!_voices[i].IsActive)
                {
                    _voices[i].NoteOn(midiNote, velocity * Preset.Volume);
                    _voices[i].Pan = Preset.Pan;
                    return i;
                }
            }

            // Voice stealing: kill voice 0 (simple round-robin stealing)
            _voices[0].Kill();
            _voices[0].NoteOn(midiNote, velocity * Preset.Volume);
            _voices[0].Pan = Preset.Pan;
            return 0;
        }

        /// <summary>
        /// Release a specific note.
        /// </summary>
        public void NoteOff(int midiNote)
        {
            for (int i = 0; i < _voices.Length; i++)
            {
                if (_voices[i].CurrentNote == midiNote && _voices[i].IsActive)
                {
                    _voices[i].NoteOff();
                }
            }
        }

        /// <summary>
        /// Release all notes.
        /// </summary>
        public void AllNotesOff()
        {
            for (int i = 0; i < _voices.Length; i++)
                _voices[i].NoteOff();
        }

        /// <summary>
        /// Kill all voices immediately (no release).
        /// </summary>
        public void Panic()
        {
            for (int i = 0; i < _voices.Length; i++)
                _voices[i].Kill();
        }

        /// <summary>
        /// Mix all active voices into a stereo sample pair.
        /// </summary>
        public void GetStereoSample(out float left, out float right)
        {
            left = 0f;
            right = 0f;

            for (int i = 0; i < _voices.Length; i++)
            {
                if (!_voices[i].IsActive) continue;

                float sample = _voices[i].NextSample();
                float pan = _voices[i].Pan;

                // Constant-power panning
                float panAngle = (pan + 1f) * 0.25f * Mathf.PI;
                left += sample * Mathf.Cos(panAngle);
                right += sample * Mathf.Sin(panAngle);
            }
        }
    }

    /// <summary>
    /// Master mixer that combines all instrument voice managers into the final audio output.
    /// Implements a simple soft-clipper limiter to prevent distortion.
    /// </summary>
    public class MasterMixer
    {
        public float MasterVolume = 0.7f;

        private List<VoiceManager> _instruments = new List<VoiceManager>();
        private float _sampleRate;

        // Simple reverb via comb filter
        private float[] _reverbBufferL, _reverbBufferR;
        private int _reverbIndex;
        private float _reverbMix = 0.15f;
        private float _reverbFeedback = 0.4f;

        public MasterMixer(float sampleRate = 48000f)
        {
            _sampleRate = sampleRate;

            // ~50ms delay for reverb
            int reverbSize = (int)(sampleRate * 0.05f);
            _reverbBufferL = new float[reverbSize];
            _reverbBufferR = new float[reverbSize];
            _reverbIndex = 0;
        }

        public VoiceManager AddInstrument(InstrumentPreset preset)
        {
            var vm = new VoiceManager(preset, _sampleRate);
            _instruments.Add(vm);
            return vm;
        }

        public List<VoiceManager> GetInstruments() => _instruments;

        /// <summary>
        /// Fill a stereo interleaved buffer (Unity's OnAudioFilterRead format).
        /// </summary>
        public void FillBuffer(float[] buffer, int channels)
        {
            int sampleFrames = buffer.Length / channels;

            for (int i = 0; i < sampleFrames; i++)
            {
                float mixL = 0f, mixR = 0f;

                foreach (var inst in _instruments)
                {
                    inst.GetStereoSample(out float l, out float r);
                    mixL += l;
                    mixR += r;
                }

                // Apply simple reverb
                float reverbL = _reverbBufferL[_reverbIndex];
                float reverbR = _reverbBufferR[_reverbIndex];
                _reverbBufferL[_reverbIndex] = mixL + reverbL * _reverbFeedback;
                _reverbBufferR[_reverbIndex] = mixR + reverbR * _reverbFeedback;
                _reverbIndex = (_reverbIndex + 1) % _reverbBufferL.Length;

                mixL += reverbL * _reverbMix;
                mixR += reverbR * _reverbMix;

                // Master volume + soft clip limiter
                mixL *= MasterVolume;
                mixR *= MasterVolume;
                mixL = SoftClip(mixL);
                mixR = SoftClip(mixR);

                // Write to interleaved buffer
                buffer[i * channels] = mixL;
                if (channels > 1)
                    buffer[i * channels + 1] = mixR;
            }
        }

        /// <summary>
        /// Soft clipper: tanh-based saturation to prevent harsh digital clipping.
        /// </summary>
        private static float SoftClip(float x)
        {
            if (x > 1.5f) return 1f;
            if (x < -1.5f) return -1f;
            return x - (x * x * x) / 3f; // Approximate tanh
        }
    }
}
