namespace OurHappyHome.Audio;

/// <summary>
/// Tiny offline synthesiser: every sound effect, ambience loop, voice babble
/// and music track in the game is generated from these building blocks at
/// start-up, so the game ships without audio files (except recorded voices).
/// </summary>
public static class Synth
{
    public const int Rate = 32000;

    public static float[] Buffer(float seconds) => new float[Math.Max(1, (int)(seconds * Rate))];

    public static float MidiToHz(float midi) => 440f * MathF.Pow(2f, (midi - 69f) / 12f);

    /// <summary>Adds src into dst at a sample offset (wrapping for seamless loops when asked).</summary>
    public static void Mix(float[] dst, float[] src, int offset, float gain = 1f, bool wrap = false)
    {
        for (int i = 0; i < src.Length; i++)
        {
            int j = offset + i;
            if (j >= dst.Length)
            {
                if (!wrap)
                {
                    break;
                }

                j %= dst.Length;
            }

            if (j >= 0)
            {
                dst[j] += src[i] * gain;
            }
        }
    }

    public static void Normalize(float[] buffer, float peak = 0.85f)
    {
        float max = 1e-6f;
        foreach (float s in buffer)
        {
            max = MathF.Max(max, MathF.Abs(s));
        }

        float g = peak / max;
        for (int i = 0; i < buffer.Length; i++)
        {
            buffer[i] = MathF.Tanh(buffer[i] * g * 1.1f) / MathF.Tanh(1.1f) * peak;
        }
    }

    /// <summary>Fades the ends of a one-shot to avoid clicks.</summary>
    public static float[] Declick(float[] b, float ms = 4f)
    {
        int n = Math.Min(b.Length / 2, (int)(ms * Rate / 1000f));
        for (int i = 0; i < n; i++)
        {
            float g = i / (float)n;
            b[i] *= g;
            b[^(i + 1)] *= g;
        }

        return b;
    }

    // ---------------------------------------------------------- instruments

    public static float[] Tone(float hz, float seconds, Func<float, float> envelope, Func<float, float> wave)
    {
        float[] b = Buffer(seconds);
        float phase = 0f;
        for (int i = 0; i < b.Length; i++)
        {
            float t = i / (float)Rate;
            phase += hz / Rate;
            b[i] = wave(phase % 1f) * envelope(t);
        }

        return b;
    }

    public static float Sine(float p) => MathF.Sin(p * MathF.Tau);

    public static float Triangle(float p) => 1f - (4f * MathF.Abs(p - 0.5f));

    public static float Saw(float p) => (2f * p) - 1f;

    public static float Square(float p) => p < 0.5f ? 1f : -1f;

    public static Func<float, float> Adsr(float attack, float decay, float sustain, float release, float length) => t =>
    {
        if (t < attack)
        {
            return t / attack;
        }

        if (t < attack + decay)
        {
            return 1f - ((1f - sustain) * ((t - attack) / decay));
        }

        if (t < length)
        {
            return sustain;
        }

        return MathF.Max(0f, sustain * (1f - ((t - length) / release)));
    };

    public static Func<float, float> Pluck(float decay) => t => (t < 0.004f ? t / 0.004f : 1f) * MathF.Exp(-t / decay);

    /// <summary>Soft electric-piano: decaying harmonics with a touch of chorus.</summary>
    public static float[] Piano(float midi, float seconds, float velocity = 1f)
    {
        float hz = MidiToHz(midi);
        float[] b = Buffer(seconds + 0.4f);
        for (int h = 1; h <= 6; h++)
        {
            float amp = 1f / MathF.Pow(h, 1.5f);
            float decay = 1.4f / (1f + (h * 0.6f));
            float detune = h == 1 ? 1.0015f : 1f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float env = (t < 0.005f ? t / 0.005f : 1f) * MathF.Exp(-t / decay) * (t > seconds ? MathF.Exp(-(t - seconds) / 0.08f) : 1f);
                b[i] += amp * env * MathF.Sin(MathF.Tau * hz * h * t * detune);
            }
        }

        Scale(b, 0.35f * velocity);
        return b;
    }

    /// <summary>Karplus-Strong plucked string (ukulele / guitar).</summary>
    public static float[] String(float midi, float seconds, Random random, float brightness = 0.5f)
    {
        float hz = MidiToHz(midi);
        int period = Math.Max(2, (int)(Rate / hz));
        float[] ring = new float[period];
        for (int i = 0; i < period; i++)
        {
            ring[i] = ((float)random.NextDouble() * 2f) - 1f;
        }

        float[] b = Buffer(seconds);
        float damping = 0.996f - ((1f - brightness) * 0.01f);
        int index = 0;
        for (int i = 0; i < b.Length; i++)
        {
            int next = (index + 1) % period;
            float v = (ring[index] + ring[next]) * 0.5f * damping;
            b[i] = ring[index];
            ring[index] = v;
            index = next;
        }

        Scale(b, 0.3f);
        return Declick(b);
    }

    /// <summary>FM bell / music box.</summary>
    public static float[] Bell(float midi, float seconds, float ratio = 3.5f)
    {
        float hz = MidiToHz(midi);
        float[] b = Buffer(seconds);
        for (int i = 0; i < b.Length; i++)
        {
            float t = i / (float)Rate;
            float env = (t < 0.002f ? t / 0.002f : 1f) * MathF.Exp(-t / (seconds * 0.35f));
            float index = 2.2f * MathF.Exp(-t * 3f);
            b[i] = MathF.Sin((MathF.Tau * hz * t) + (index * MathF.Sin(MathF.Tau * hz * ratio * t))) * env * 0.3f;
        }

        return b;
    }

    /// <summary>Warm pad: detuned saws through a gentle low-pass.</summary>
    public static float[] Pad(float[] midis, float seconds, float cutoff = 0.06f)
    {
        float[] b = Buffer(seconds);
        foreach (float midi in midis)
        {
            float hz = MidiToHz(midi);
            for (int voice = 0; voice < 3; voice++)
            {
                float detune = 1f + ((voice - 1) * 0.004f);
                float phase = voice * 0.33f;
                float lp = 0f;
                for (int i = 0; i < b.Length; i++)
                {
                    float t = i / (float)Rate;
                    phase += hz * detune / Rate;
                    float raw = Saw(phase % 1f);
                    lp += (raw - lp) * cutoff;
                    float env = MathF.Min(1f, t / 0.35f) * MathF.Min(1f, (seconds - t) / 0.4f);
                    b[i] += lp * env * 0.06f;
                }
            }
        }

        return b;
    }

    public static float[] Bass(float midi, float seconds)
    {
        float hz = MidiToHz(midi);
        return Tone(hz, seconds, t => (t < 0.01f ? t / 0.01f : 1f) * MathF.Exp(-t / 0.5f) * MathF.Min(1f, (seconds - t) / 0.03f + 0.001f),
            p => (Sine(p) * 0.8f) + (Triangle(p) * 0.25f)).Select(v => v * 0.45f).ToArray();
    }

    public static float[] Lead(float midi, float seconds, float bright = 0.15f)
    {
        float hz = MidiToHz(midi);
        float[] b = Buffer(seconds + 0.1f);
        float lp = 0f;
        float phase = 0f;
        for (int i = 0; i < b.Length; i++)
        {
            float t = i / (float)Rate;
            float vibrato = 1f + (0.004f * MathF.Sin(MathF.Tau * 5.5f * t) * MathF.Min(1f, t * 4f));
            phase += hz * vibrato / Rate;
            float raw = (Saw(phase % 1f) * 0.6f) + (Square((phase * 0.5f) % 1f) * 0.2f);
            lp += (raw - lp) * (bright + (0.2f * MathF.Exp(-t * 6f)));
            float env = MathF.Min(1f, t / 0.02f) * (t > seconds ? MathF.Exp(-(t - seconds) / 0.05f) : 0.85f + (0.15f * MathF.Exp(-t * 4f)));
            b[i] = lp * env * 0.22f;
        }

        return b;
    }

    // -------------------------------------------------------------- drums

    public static float[] Kick()
    {
        float[] b = Buffer(0.35f);
        float phase = 0f;
        for (int i = 0; i < b.Length; i++)
        {
            float t = i / (float)Rate;
            float hz = 45f + (90f * MathF.Exp(-t * 28f));
            phase += hz / Rate;
            b[i] = MathF.Sin(phase * MathF.Tau) * MathF.Exp(-t * 9f) * 0.9f;
        }

        return b;
    }

    public static float[] Snare(Random r)
    {
        float[] b = Buffer(0.22f);
        float hp = 0f;
        float prev = 0f;
        for (int i = 0; i < b.Length; i++)
        {
            float t = i / (float)Rate;
            float n = ((float)r.NextDouble() * 2f) - 1f;
            hp = 0.7f * (hp + n - prev);
            prev = n;
            b[i] = ((hp * 0.6f) + (MathF.Sin(MathF.Tau * 190f * t) * 0.35f)) * MathF.Exp(-t * 18f) * 0.5f;
        }

        return b;
    }

    public static float[] Hat(Random r, float length = 0.05f, float gain = 0.25f)
    {
        float[] b = Buffer(length + 0.02f);
        float prev = 0f;
        for (int i = 0; i < b.Length; i++)
        {
            float t = i / (float)Rate;
            float n = ((float)r.NextDouble() * 2f) - 1f;
            float hp = n - prev;
            prev = n;
            b[i] = hp * MathF.Exp(-t / (length * 0.35f)) * gain;
        }

        return b;
    }

    public static float[] Clap(Random r)
    {
        float[] b = Buffer(0.25f);
        for (int burst = 0; burst < 3; burst++)
        {
            int start = (int)(burst * 0.012f * Rate);
            for (int i = 0; i < b.Length - start; i++)
            {
                float t = i / (float)Rate;
                b[start + i] += (((float)r.NextDouble() * 2f) - 1f) * MathF.Exp(-t * (burst == 2 ? 14f : 60f)) * 0.3f;
            }
        }

        return b;
    }

    // -------------------------------------------------------------- noise

    /// <summary>Filtered noise: low-pass coefficient 0-1 (smaller = darker).</summary>
    public static float[] Noise(float seconds, Random r, float lowpass, Func<float, float> envelope, float gain = 0.5f)
    {
        float[] b = Buffer(seconds);
        float lp = 0f;
        for (int i = 0; i < b.Length; i++)
        {
            float t = i / (float)Rate;
            float n = ((float)r.NextDouble() * 2f) - 1f;
            lp += (n - lp) * lowpass;
            b[i] = lp * envelope(t) * gain;
        }

        return b;
    }

    public static void Scale(float[] b, float g)
    {
        for (int i = 0; i < b.Length; i++)
        {
            b[i] *= g;
        }
    }

    /// <summary>Exponential pitch sweep (chirps, zips, slide whistles).</summary>
    public static float[] Sweep(float fromHz, float toHz, float seconds, Func<float, float> wave, float gain = 0.4f)
    {
        float[] b = Buffer(seconds);
        float phase = 0f;
        for (int i = 0; i < b.Length; i++)
        {
            float t = i / (float)Rate;
            float k = t / seconds;
            float hz = fromHz * MathF.Pow(toHz / fromHz, k);
            phase += hz / Rate;
            b[i] = wave(phase % 1f) * MathF.Sin(MathF.PI * MathF.Min(1f, k * 1.05f)) * gain;
        }

        return b;
    }

    /// <summary>Concatenates buffers with a gap (in seconds) between them.</summary>
    public static float[] Sequence(params (float[] Sound, float At)[] parts)
    {
        float length = parts.Max(p => p.At + (p.Sound.Length / (float)Rate));
        float[] b = Buffer(length + 0.05f);
        foreach ((float[] s, float at) in parts)
        {
            Mix(b, s, (int)(at * Rate));
        }

        return b;
    }
}
