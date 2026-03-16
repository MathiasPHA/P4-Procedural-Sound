using System;
using UnityEngine;

namespace ProceduralMusic.Synthesis
{
    public enum Waveform
    {
        Sine,
        Saw,
        Square,
        Triangle,
        Noise
    }

    /// <summary>
    /// Band-limited oscillator with PolyBLEP antialiasing.
    /// </summary>
    public class Oscillator
    {
        public Waveform Waveform = Waveform.Sine;
        public float Frequency = 440f;
        public float Phase;
        public float Detune; // In cents

        private float _sampleRate;
        private System.Random _noiseRng = new System.Random();

        public Oscillator(float sampleRate = 48000f)
        {
            _sampleRate = sampleRate;
        }

        public float NextSample()
        {
            float freq = Frequency * Mathf.Pow(2f, Detune / 1200f);
            float phaseIncrement = freq / _sampleRate;
            float sample;

            switch (Waveform)
            {
                case Waveform.Sine:
                    sample = Mathf.Sin(2f * Mathf.PI * Phase);
                    break;
                case Waveform.Saw:
                    sample = 2f * Phase - 1f;
                    sample -= PolyBLEP(Phase, phaseIncrement);
                    break;
                case Waveform.Square:
                    sample = Phase < 0.5f ? 1f : -1f;
                    sample += PolyBLEP(Phase, phaseIncrement);
                    sample -= PolyBLEP((Phase + 0.5f) % 1f, phaseIncrement);
                    break;
                case Waveform.Triangle:
                    sample = 2f * Mathf.Abs(2f * Phase - 1f) - 1f;
                    break;
                case Waveform.Noise:
                    sample = (float)(_noiseRng.NextDouble() * 2.0 - 1.0);
                    break;
                default:
                    sample = 0f;
                    break;
            }

            Phase += phaseIncrement;
            if (Phase >= 1f) Phase -= 1f;
            return sample;
        }

        private static float PolyBLEP(float t, float dt)
        {
            if (t < dt) { t /= dt; return t + t - t * t - 1f; }
            else if (t > 1f - dt) { t = (t - 1f) / dt; return t * t + t + t + 1f; }
            return 0f;
        }

        public void Reset() { Phase = 0f; }
    }

    /// <summary>
    /// ADSR envelope with exponential curves.
    /// </summary>
    public class ADSREnvelope
    {
        public float Attack = 0.01f;
        public float Decay = 0.1f;
        public float Sustain = 0.7f;
        public float Release = 0.3f;

        private enum Stage { Idle, Attack, Decay, SustainStage, Release }
        private Stage _stage = Stage.Idle;
        private float _level;
        private float _releaseLevel;
        private float _time;
        private float _sampleRate;

        public bool IsActive => _stage != Stage.Idle;
        public float Level => _level;

        public ADSREnvelope(float sampleRate = 48000f) { _sampleRate = sampleRate; }

        public void NoteOn() { _stage = Stage.Attack; _time = 0f; }

        public void NoteOff()
        {
            if (_stage != Stage.Idle) { _releaseLevel = _level; _stage = Stage.Release; _time = 0f; }
        }

        public void Kill() { _stage = Stage.Idle; _level = 0f; }

        public float NextSample()
        {
            float dt = 1f / _sampleRate;
            switch (_stage)
            {
                case Stage.Attack:
                    _time += dt;
                    _level = Mathf.Clamp01(_time / Mathf.Max(Attack, 0.001f));
                    if (_level >= 1f) { _level = 1f; _stage = Stage.Decay; _time = 0f; }
                    break;
                case Stage.Decay:
                    _time += dt;
                    float dp = Mathf.Clamp01(_time / Mathf.Max(Decay, 0.001f));
                    _level = Sustain + (1f - Sustain) * Mathf.Exp(-5f * dp);
                    if (dp >= 1f) { _level = Sustain; _stage = Stage.SustainStage; }
                    break;
                case Stage.SustainStage:
                    _level = Sustain;
                    break;
                case Stage.Release:
                    _time += dt;
                    float rp = Mathf.Clamp01(_time / Mathf.Max(Release, 0.001f));
                    _level = _releaseLevel * Mathf.Exp(-5f * rp);
                    if (rp >= 1f || _level < 0.001f) { _level = 0f; _stage = Stage.Idle; }
                    break;
                case Stage.Idle:
                    _level = 0f;
                    break;
            }
            return _level;
        }
    }

    /// <summary>
    /// Resonant low-pass filter (state variable).
    /// </summary>
    public class LowPassFilter
    {
        public float Cutoff = 8000f;
        public float Resonance = 0.5f;

        private float _low, _band, _high;
        private float _sampleRate;

        public LowPassFilter(float sampleRate = 48000f) { _sampleRate = sampleRate; }

        public float Process(float input)
        {
            float f = 2f * Mathf.Sin(Mathf.PI * Mathf.Min(Cutoff, _sampleRate * 0.45f) / _sampleRate);
            float q = 1f - Resonance * 0.95f;
            _high = input - _low - q * _band;
            _band += f * _high;
            _low += f * _band;
            return _low;
        }

        public void Reset() { _low = _band = _high = 0f; }
    }

    /// <summary>
    /// Two-operator FM synthesizer.
    /// </summary>
    public class FMOperator
    {
        public float CarrierFrequency = 440f;
        public float ModulatorRatio = 2f;
        public float ModulationIndex = 1f;
        public float ModulationIndexEnvAmount = 0.5f;

        private Oscillator _carrier;
        private Oscillator _modulator;

        public FMOperator(float sampleRate = 48000f)
        {
            _carrier = new Oscillator(sampleRate) { Waveform = Waveform.Sine };
            _modulator = new Oscillator(sampleRate) { Waveform = Waveform.Sine };
        }

        public void SetFrequency(float freq)
        {
            CarrierFrequency = freq;
            _carrier.Frequency = freq;
            _modulator.Frequency = freq * ModulatorRatio;
        }

        public float NextSample(float envelope = 1f)
        {
            float dynamicIndex = ModulationIndex * (1f - ModulationIndexEnvAmount + ModulationIndexEnvAmount * envelope);
            float modOutput = _modulator.NextSample();
            float modAmount = modOutput * dynamicIndex * _modulator.Frequency;
            _carrier.Frequency = CarrierFrequency + modAmount;
            return _carrier.NextSample();
        }

        public void Reset() { _carrier.Reset(); _modulator.Reset(); }
    }

    /// <summary>
    /// Portamento (pitch glide) helper. Smoothly transitions between frequencies.
    /// </summary>
    public class Portamento
    {
        public float GlideTime = 0.08f; // Seconds for full glide

        private float _currentFreq;
        private float _targetFreq;
        private float _glideRate;
        private float _sampleRate;
        private bool _initialized;

        public float CurrentFrequency => _currentFreq;

        public Portamento(float sampleRate = 48000f)
        {
            _sampleRate = sampleRate;
        }

        public void SetTarget(float freq, bool immediate = false)
        {
            _targetFreq = freq;
            if (!_initialized || immediate)
            {
                _currentFreq = freq;
                _initialized = true;
            }
            // Rate: how fast to move per sample (in log space for even pitch glide)
            float glidesamples = Mathf.Max(GlideTime * _sampleRate, 1f);
            _glideRate = 1f / glidesamples;
        }

        public float NextSample()
        {
            if (Mathf.Abs(_currentFreq - _targetFreq) > 0.01f)
            {
                // Glide in log-frequency space for perceptually even pitch slide
                float logCurrent = Mathf.Log(_currentFreq);
                float logTarget = Mathf.Log(_targetFreq);
                float logNew = logCurrent + (logTarget - logCurrent) * _glideRate * 8f;
                _currentFreq = Mathf.Exp(logNew);

                // Snap when very close
                if (Mathf.Abs(_currentFreq - _targetFreq) < 0.5f)
                    _currentFreq = _targetFreq;
            }
            return _currentFreq;
        }
    }

    /// <summary>
    /// LFO for vibrato / tremolo effects.
    /// </summary>
    public class LFO
    {
        public float Rate = 5f;      // Hz
        public float Depth = 0.005f; // Vibrato: ~5 cents. Tremolo: 0-1 amount
        public float Phase;

        private float _sampleRate;

        public LFO(float sampleRate = 48000f) { _sampleRate = sampleRate; }

        /// <summary>
        /// Returns a value oscillating between -Depth and +Depth.
        /// </summary>
        public float NextSample()
        {
            Phase += Rate / _sampleRate;
            if (Phase >= 1f) Phase -= 1f;
            return Mathf.Sin(2f * Mathf.PI * Phase) * Depth;
        }
    }

    /// <summary>
    /// A complete synthesizer voice with support for:
    ///   - Subtractive (saw/square + filter)
    ///   - FM (2-operator)
    ///   - Additive (harmonic partials)
    ///   - Percussion (noise + sine pitch envelope)
    ///   - StringEnsemble (layered detuned saws + filter + vibrato, for string sections)
    ///   - LegatoLead (portamento + vibrato, smooth melodic lead)
    /// </summary>
    public class SynthVoice
    {
        public enum SynthType { Subtractive, FM, Additive, Percussion, StringEnsemble, LegatoLead }

        public SynthType Type = SynthType.FM;
        public bool IsActive => _ampEnvelope.IsActive;
        public int CurrentNote { get; private set; } = -1;
        public float Volume = 1f;
        public float Pan = 0f;

        // Subtractive components
        private Oscillator _oscillator;
        private LowPassFilter _filter;
        private float _filterBaseFreq = 2000f;
        private float _filterEnvAmount = 4000f;

        // FM components
        private FMOperator _fmOperator;

        // Additive components
        private Oscillator[] _additiveOscs;
        private float[] _additiveAmps;

        // Percussion
        private Oscillator _percOsc;
        private Oscillator _noiseOsc;
        private float _percPitchEnv;
        private float _percPitchDecay = 0.98f;

        // String ensemble: 4 detuned saw oscillators + filter + vibrato
        private Oscillator[] _stringOscs;
        private LowPassFilter _stringFilter;
        private LFO _stringVibrato;
        private float _stringVibratoDepth = 0.003f;

        // Legato lead / Flute: portamento + delayed vibrato + breathy additive
        private Portamento _portamento;
        private LFO _legatoVibrato;
        private Oscillator[] _flutePartials;     // Fundamental + odd harmonics (flute spectrum)
        private Oscillator _fluteBreathNoise;    // Breath noise component
        private LowPassFilter _fluteBreathFilter;
        private LowPassFilter _legatoFilter;
        private bool _legatoFirstNote = true;
        private float _fluteVibratoFadeIn;       // Vibrato fades in over time (realistic)
        private float _fluteBreathAmount = 0.08f; // How much breath noise to mix in

        // Shared
        private ADSREnvelope _ampEnvelope;
        private float _sampleRate;

        public SynthVoice(float sampleRate = 48000f)
        {
            _sampleRate = sampleRate;
            _oscillator = new Oscillator(sampleRate);
            _filter = new LowPassFilter(sampleRate);
            _fmOperator = new FMOperator(sampleRate);
            _ampEnvelope = new ADSREnvelope(sampleRate);

            // Additive (6 partials)
            _additiveOscs = new Oscillator[6];
            _additiveAmps = new float[] { 1f, 0.5f, 0.25f, 0.15f, 0.08f, 0.04f };
            for (int i = 0; i < 6; i++)
                _additiveOscs[i] = new Oscillator(sampleRate) { Waveform = Waveform.Sine };

            // Percussion
            _percOsc = new Oscillator(sampleRate) { Waveform = Waveform.Sine };
            _noiseOsc = new Oscillator(sampleRate) { Waveform = Waveform.Noise };

            // String ensemble: 4 saw oscillators with different detunings
            _stringOscs = new Oscillator[4];
            for (int i = 0; i < 4; i++)
                _stringOscs[i] = new Oscillator(sampleRate) { Waveform = Waveform.Saw };
            _stringFilter = new LowPassFilter(sampleRate) { Cutoff = 3000f, Resonance = 0.15f };
            _stringVibrato = new LFO(sampleRate) { Rate = 5.2f, Depth = 0.003f };

            // Legato lead / Flute
            _portamento = new Portamento(sampleRate) { GlideTime = 0.08f };
            _legatoVibrato = new LFO(sampleRate) { Rate = 5.5f, Depth = 0.004f };
            _legatoFilter = new LowPassFilter(sampleRate) { Cutoff = 4000f, Resonance = 0.1f };

            // Flute spectrum: fundamental + weak odd harmonics (flutes are almost pure sine)
            _flutePartials = new Oscillator[4];
            for (int i = 0; i < 4; i++)
                _flutePartials[i] = new Oscillator(sampleRate) { Waveform = Waveform.Sine };

            // Breath noise: filtered noise mixed in for airiness
            _fluteBreathNoise = new Oscillator(sampleRate) { Waveform = Waveform.Noise };
            _fluteBreathFilter = new LowPassFilter(sampleRate) { Cutoff = 2500f, Resonance = 0.3f };
        }

        public void Configure(SynthType type, float attack, float decay, float sustain, float release,
            Waveform waveform = Waveform.Saw, float filterCutoff = 2000f, float filterEnvAmt = 4000f,
            float fmRatio = 2f, float fmIndex = 1f, float fmEnvAmt = 0.5f,
            float portamentoTime = 0.08f, float vibratoRate = 5.5f, float vibratoDepth = 0.004f)
        {
            Type = type;
            _ampEnvelope.Attack = attack;
            _ampEnvelope.Decay = decay;
            _ampEnvelope.Sustain = sustain;
            _ampEnvelope.Release = release;
            _oscillator.Waveform = waveform;
            _filterBaseFreq = filterCutoff;
            _filterEnvAmount = filterEnvAmt;
            _fmOperator.ModulatorRatio = fmRatio;
            _fmOperator.ModulationIndex = fmIndex;
            _fmOperator.ModulationIndexEnvAmount = fmEnvAmt;
            _portamento.GlideTime = portamentoTime;
            _legatoVibrato.Rate = vibratoRate;
            _legatoVibrato.Depth = vibratoDepth;
            _fluteBreathFilter.Cutoff = filterCutoff * 0.6f; // Breath noise below the tone
            _stringVibrato.Rate = vibratoRate;
            _stringVibrato.Depth = vibratoDepth;
            _stringFilter.Cutoff = filterCutoff;
        }

        public void NoteOn(int midiNote, float velocity = 1f)
        {
            CurrentNote = midiNote;
            Volume = velocity;
            float freq = 440f * Mathf.Pow(2f, (midiNote - 69) / 12f);

            switch (Type)
            {
                case SynthType.Subtractive:
                    _oscillator.Frequency = freq;
                    _oscillator.Reset();
                    _filter.Reset();
                    _ampEnvelope.NoteOn();
                    break;

                case SynthType.FM:
                    _fmOperator.SetFrequency(freq);
                    _fmOperator.Reset();
                    _ampEnvelope.NoteOn();
                    break;

                case SynthType.Additive:
                    for (int i = 0; i < _additiveOscs.Length; i++)
                    {
                        _additiveOscs[i].Frequency = freq * (i + 1);
                        _additiveOscs[i].Detune = (i > 0) ? (UnityEngine.Random.value - 0.5f) * 8f : 0f;
                        _additiveOscs[i].Reset();
                    }
                    _ampEnvelope.NoteOn();
                    break;

                case SynthType.Percussion:
                    _percOsc.Frequency = freq;
                    _percPitchEnv = freq * 4f;
                    _noiseOsc.Reset();
                    _ampEnvelope.NoteOn();
                    break;

                case SynthType.StringEnsemble:
                    // Detuned saw ensemble: ±5-12 cents spread for rich chorus effect
                    float[] stringDetunes = { -10f, -3f, 4f, 11f };
                    for (int i = 0; i < _stringOscs.Length; i++)
                    {
                        _stringOscs[i].Frequency = freq;
                        _stringOscs[i].Detune = stringDetunes[i];
                        // Don't reset phase — keeps it smooth on retrigger
                    }
                    _stringFilter.Cutoff = _filterBaseFreq;
                    _ampEnvelope.NoteOn();
                    break;

                case SynthType.LegatoLead:
                    // Portamento: glide from previous note
                    bool isLegato = !_legatoFirstNote && _ampEnvelope.IsActive;
                    _portamento.SetTarget(freq, _legatoFirstNote);
                    _legatoFilter.Cutoff = _filterBaseFreq;

                    // Set flute partial frequencies: fundamental + odd harmonics
                    // Flute has a strong fundamental, weak 2nd harmonic, and progressively
                    // weaker odd harmonics — this is what gives it that pure, airy quality
                    if (_legatoFirstNote || !isLegato)
                    {
                        _flutePartials[0].Frequency = freq;          // Fundamental (strong)
                        _flutePartials[1].Frequency = freq * 2f;     // 2nd harmonic (weak)
                        _flutePartials[2].Frequency = freq * 3f;     // 3rd harmonic (medium-weak)
                        _flutePartials[3].Frequency = freq * 4f;     // 4th (very weak)
                    }

                    // Reset vibrato fade-in on new phrase, but not on legato transitions
                    if (!isLegato)
                        _fluteVibratoFadeIn = 0f;

                    _legatoFirstNote = false;

                    if (!isLegato)
                        _ampEnvelope.NoteOn();
                    break;
            }

            if (Type != SynthType.LegatoLead && Type != SynthType.StringEnsemble)
            {
                // Already called above for these types
            }
        }

        public void NoteOff()
        {
            _ampEnvelope.NoteOff();
        }

        public void Kill()
        {
            _ampEnvelope.Kill();
            CurrentNote = -1;
            if (Type == SynthType.LegatoLead)
                _legatoFirstNote = true;
        }

        public float NextSample()
        {
            if (!IsActive) return 0f;

            float env = _ampEnvelope.NextSample();
            float sample;

            switch (Type)
            {
                case SynthType.Subtractive:
                    sample = _oscillator.NextSample();
                    _filter.Cutoff = _filterBaseFreq + _filterEnvAmount * env;
                    sample = _filter.Process(sample);
                    break;

                case SynthType.FM:
                    sample = _fmOperator.NextSample(env);
                    break;

                case SynthType.Additive:
                    sample = 0f;
                    for (int i = 0; i < _additiveOscs.Length; i++)
                        sample += _additiveOscs[i].NextSample() * _additiveAmps[i];
                    sample *= 0.4f;
                    break;

                case SynthType.Percussion:
                    _percPitchEnv *= _percPitchDecay;
                    _percOsc.Frequency = _percPitchEnv;
                    sample = _percOsc.NextSample() * 0.6f + _noiseOsc.NextSample() * 0.4f;
                    break;

                case SynthType.StringEnsemble:
                    sample = GenerateStringEnsemble(env);
                    break;

                case SynthType.LegatoLead:
                    sample = GenerateLegatoLead(env);
                    break;

                default:
                    sample = 0f;
                    break;
            }

            return sample * env * Volume;
        }

        /// <summary>
        /// String ensemble: 4 detuned saws through a warm low-pass filter with vibrato.
        /// Produces a rich, warm, orchestral-ish texture.
        /// </summary>
        private float GenerateStringEnsemble(float env)
        {
            float vibrato = _stringVibrato.NextSample();
            float sample = 0f;

            for (int i = 0; i < _stringOscs.Length; i++)
            {
                // Apply vibrato as a pitch modulation (different phase per oscillator for width)
                float oscVibrato = vibrato * (0.8f + i * 0.15f);
                float baseFreq = _stringOscs[i].Frequency;
                _stringOscs[i].Frequency = baseFreq * (1f + oscVibrato);
                sample += _stringOscs[i].NextSample();
                _stringOscs[i].Frequency = baseFreq; // Restore
            }

            sample *= 0.25f; // Normalize 4 oscillators

            // Warm filter: cutoff opens slightly with envelope for expressiveness
            _stringFilter.Cutoff = _filterBaseFreq + _filterEnvAmount * env * 0.3f;
            sample = _stringFilter.Process(sample);

            return sample;
        }

        /// <summary>
        /// Flute-like legato lead:
        ///  - Strong sine fundamental + weak odd harmonics (pure, hollow tone)
        ///  - Filtered breath noise mixed in for airiness
        ///  - Vibrato that fades in gradually after note onset (like a real flutist)
        ///  - Portamento for smooth legato connections
        ///  - Gentle low-pass for warmth
        /// </summary>
        private float GenerateLegatoLead(float env)
        {
            // Get gliding frequency from portamento
            float freq = _portamento.NextSample();

            // Vibrato fades in over ~0.3 seconds after note start (realistic breath control)
            _fluteVibratoFadeIn = Mathf.Min(_fluteVibratoFadeIn + 1f / (_sampleRate * 0.3f), 1f);
            float vibrato = _legatoVibrato.NextSample() * _fluteVibratoFadeIn;
            float vibratoFreq = freq * (1f + vibrato);

            // Update partial frequencies (portamento glides them all together)
            _flutePartials[0].Frequency = vibratoFreq;
            _flutePartials[1].Frequency = vibratoFreq * 2f;
            _flutePartials[2].Frequency = vibratoFreq * 3f;
            _flutePartials[3].Frequency = vibratoFreq * 4f;

            // Flute harmonic amplitudes: fundamental dominant, odd harmonics present but quiet
            // This spectrum is what makes a flute sound hollow and pure vs. a violin or trumpet
            float sample = _flutePartials[0].NextSample() * 1.0f    // Fundamental: strong
                         + _flutePartials[1].NextSample() * 0.12f   // 2nd: very weak (flute characteristic)
                         + _flutePartials[2].NextSample() * 0.18f   // 3rd: slightly stronger (odd harmonic)
                         + _flutePartials[3].NextSample() * 0.05f;  // 4th: barely there
            sample *= 0.7f; // Normalize

            // Breath noise: filtered noise adds the airy, breathy quality
            // More prominent during attack, fades during sustain
            float breathEnv = Mathf.Lerp(_fluteBreathAmount * 2f, _fluteBreathAmount, env);
            float breath = _fluteBreathNoise.NextSample();
            // Pitch-track the breath filter (higher notes = higher breath noise)
            _fluteBreathFilter.Cutoff = freq * 1.5f + 500f;
            breath = _fluteBreathFilter.Process(breath) * breathEnv;
            sample += breath;

            // Gentle overall filtering for warmth
            _legatoFilter.Cutoff = _filterBaseFreq + _filterEnvAmount * env * 0.4f;
            sample = _legatoFilter.Process(sample);

            return sample;
        }
    }
}
