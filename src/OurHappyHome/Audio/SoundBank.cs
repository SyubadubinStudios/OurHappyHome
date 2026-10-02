using OurHappyHome.Core.Family;
using static OurHappyHome.Audio.Synth;

namespace OurHappyHome.Audio;

/// <summary>Procedurally generated sound effects, ambience loops and babble voices.</summary>
public static class SoundBank
{
    public static Dictionary<string, float[]> Effects()
    {
        Random r = new(42);
        Dictionary<string, float[]> s = [];

        s["click"] = Declick(Tone(1400f, 0.04f, t => MathF.Exp(-t * 90f), Sine).Select(v => v * 0.4f).ToArray());
        s["hover"] = Declick(Tone(2200f, 0.03f, t => MathF.Exp(-t * 120f), Sine).Select(v => v * 0.15f).ToArray());
        s["objective"] = Sequence((Bell(79, 0.6f), 0f), (Bell(84, 0.8f), 0.09f));
        s["success"] = Sequence((Piano(72, 0.2f), 0f), (Piano(76, 0.2f), 0.1f), (Piano(79, 0.2f), 0.2f), (Piano(84, 0.6f), 0.3f));
        s["achievement"] = Sequence((Bell(76, 0.5f), 0f), (Bell(81, 0.5f), 0.12f), (Bell(88, 1.2f), 0.24f), (Piano(64, 1f, 0.6f), 0.24f));
        s["fanfare"] = Sequence((Lead(67, 0.18f), 0f), (Lead(67, 0.12f), 0.2f), (Lead(72, 0.5f), 0.34f), (Lead(76, 0.18f), 0.9f), (Lead(79, 0.9f), 1.1f), (Pad([60, 64, 67, 72], 2.2f), 0f));
        s["alert"] = Sequence((Tone(880f, 0.15f, Adsr(0.005f, 0.05f, 0.6f, 0.05f, 0.1f), Square).Select(v => v * 0.18f).ToArray(), 0f), (Tone(660f, 0.2f, Adsr(0.005f, 0.05f, 0.6f, 0.05f, 0.15f), Square).Select(v => v * 0.18f).ToArray(), 0.18f));
        s["coins"] = Sequence((Bell(88, 0.3f, 2f), 0f), (Bell(95, 0.5f, 2f), 0.07f));
        s["register"] = Sequence((Bell(84, 0.25f, 2f), 0f), (Bell(91, 0.6f, 2f), 0.06f), (Noise(0.06f, r, 0.6f, t => MathF.Exp(-t * 60f), 0.3f), 0f));
        s["camera"] = Sequence((Noise(0.03f, r, 0.9f, t => 1f, 0.4f), 0f), (Noise(0.08f, r, 0.5f, t => MathF.Exp(-t * 40f), 0.35f), 0.06f));
        s["hug"] = Sequence((Bell(72, 0.5f, 1f), 0f), (Bell(79, 0.8f, 1f), 0.12f));
        s["cheer"] = Sequence((Sweep(400, 900, 0.3f, Triangle, 0.2f), 0f), (Clap(r), 0.05f), (Clap(r), 0.2f), (Clap(r), 0.32f));
        s["sizzle"] = Noise(1.6f, r, 0.8f, t => (0.5f + (0.5f * MathF.Abs(MathF.Sin(t * 37f)))) * MathF.Min(1f, (1.6f - t) * 4f), 0.25f);
        s["tv"] = Babble(r, 260f, 2.2f, 0.18f);
        s["shower"] = Noise(2.5f, r, 0.55f, t => MathF.Min(1f, t * 5f) * MathF.Min(1f, (2.5f - t) * 3f), 0.3f);
        s["piano"] = Sequence((Piano(67, 0.3f), 0f), (Piano(71, 0.3f), 0.25f), (Piano(74, 0.3f), 0.5f), (Piano(79, 0.8f), 0.75f));
        s["thunder"] = Thunder(r);
        s["bark"] = Sequence((Bark(r, 420f), 0f), (Bark(r, 390f), 0.28f));
        s["bark-happy"] = Bark(r, 520f);
        s["meow"] = Declick(Sweep(560, 820, 0.18f, Triangle, 0.25f).Concat(Sweep(820, 480, 0.35f, Triangle, 0.25f)).ToArray());
        s["monkey"] = Sequence((Sweep(700, 1200, 0.08f, Triangle, 0.25f), 0f), (Sweep(800, 1300, 0.08f, Triangle, 0.25f), 0.1f), (Sweep(700, 1400, 0.12f, Triangle, 0.25f), 0.2f));
        s["boar"] = Noise(0.5f, r, 0.08f, t => MathF.Abs(MathF.Sin(t * 30f)) * MathF.Exp(-t * 3f), 0.9f);
        s["glass"] = Sequence((Noise(0.25f, r, 0.95f, t => MathF.Exp(-t * 18f), 0.5f), 0f), (Bell(98, 0.4f, 7.1f), 0f), (Bell(101, 0.3f, 5.3f), 0.05f), (Bell(95, 0.3f, 6.7f), 0.11f));
        s["break"] = Sequence((Noise(0.3f, r, 0.3f, t => MathF.Exp(-t * 14f), 0.8f), 0f), (Tone(90f, 0.3f, t => MathF.Exp(-t * 12f), Sine).Select(v => v * 0.6f).ToArray(), 0f));
        s["drip"] = Declick(Sweep(900, 1800, 0.06f, Sine, 0.35f));
        s["splash"] = Noise(0.6f, r, 0.6f, t => MathF.Exp(-t * 7f) * MathF.Min(1f, t * 60f), 0.6f);
        s["flood"] = Noise(2.5f, r, 0.25f, t => MathF.Min(1f, t) * MathF.Min(1f, (2.5f - t) * 2f), 0.6f);
        s["lock"] = Sequence((Noise(0.03f, r, 0.9f, t => MathF.Exp(-t * 120f), 0.6f), 0f), (Noise(0.04f, r, 0.7f, t => MathF.Exp(-t * 90f), 0.6f), 0.09f));
        s["switch"] = Noise(0.03f, r, 0.95f, t => MathF.Exp(-t * 150f), 0.6f);
        s["window"] = Noise(0.4f, r, 0.2f, t => MathF.Sin(MathF.PI * t / 0.4f), 0.5f);
        s["beep"] = Declick(Tone(1800f, 0.12f, t => 1f, Square).Select(v => v * 0.12f).ToArray());
        s["alarm-beep"] = Sequence((Tone(3100f, 0.1f, t => 1f, Square).Select(v => v * 0.08f).ToArray(), 0f), (Tone(3100f, 0.1f, t => 1f, Square).Select(v => v * 0.08f).ToArray(), 0.2f), (Tone(3100f, 0.1f, t => 1f, Square).Select(v => v * 0.08f).ToArray(), 0.4f));
        s["alarm"] = Siren(r, 950f, 1400f, 2.5f);
        s["fire-alarm"] = Sequence((s["alarm-beep"], 0f), (s["alarm-beep"], 0.8f), (s["alarm-beep"], 1.6f));
        s["siren"] = Siren(r, 700f, 1000f, 3f);
        s["extinguisher"] = Noise(1.1f, r, 0.9f, t => MathF.Min(1f, t * 20f) * MathF.Min(1f, (1.1f - t) * 5f), 0.45f);
        s["debris"] = Sequence((Noise(0.3f, r, 0.12f, t => MathF.Exp(-t * 10f), 1f), 0f), (Noise(0.2f, r, 0.2f, t => MathF.Exp(-t * 14f), 0.6f), 0.15f));
        s["creak"] = Declick(Sweep(160, 120, 0.7f, Saw, 0.12f));
        s["power-down"] = Declick(Sweep(500, 50, 0.9f, Saw, 0.18f));
        s["power-up"] = Declick(Sweep(60, 600, 0.7f, Saw, 0.14f));
        s["build"] = Sequence((Knock(r), 0f), (Knock(r), 0.18f), (Knock(r), 0.36f), (Bell(84, 0.4f, 2f), 0.5f));
        s["place"] = Knock(r);
        s["paint"] = Noise(0.5f, r, 0.35f, t => MathF.Sin(MathF.PI * t / 0.5f), 0.35f);
        s["rope"] = Noise(0.35f, r, 0.5f, t => MathF.Sin(MathF.PI * t / 0.35f), 0.3f);
        s["footstep"] = Noise(0.07f, r, 0.25f, t => MathF.Exp(-t * 60f), 0.5f);
        s["door"] = Sequence((s["creak"], 0f), (Knock(r), 0.55f));
        s["page"] = Noise(0.2f, r, 0.6f, t => MathF.Sin(MathF.PI * t / 0.2f), 0.2f);
        s["pop"] = Declick(Sweep(400, 1200, 0.08f, Sine, 0.4f));
        s["whoosh"] = Noise(0.4f, r, 0.3f, t => MathF.Sin(MathF.PI * t / 0.4f), 0.4f);
        s["wrong"] = Sequence((Tone(220f, 0.18f, Adsr(0.01f, 0.05f, 0.7f, 0.05f, 0.14f), Square).Select(v => v * 0.12f).ToArray(), 0f), (Tone(180f, 0.3f, Adsr(0.01f, 0.05f, 0.7f, 0.1f, 0.22f), Square).Select(v => v * 0.12f).ToArray(), 0.16f));
        s["correct"] = Sequence((Bell(84, 0.25f, 1f), 0f), (Bell(91, 0.4f, 1f), 0.08f));
        s["fail"] = Sequence((Piano(67, 0.3f), 0f), (Piano(63, 0.3f), 0.3f), (Piano(60, 1.2f), 0.6f));
        return s;
    }

    public static Dictionary<string, float[]> Ambience()
    {
        Random r = new(7);
        Dictionary<string, float[]> a = [];
        a["rain"] = RainLoop(r);
        a["wind"] = Noise(8f, r, 0.03f, t => 0.55f + (0.45f * MathF.Sin(t * MathF.Tau / 8f * 2f)), 1.4f);
        a["birds"] = BirdsLoop(r);
        a["crickets"] = CricketsLoop(r);
        a["fire"] = Noise(4f, r, 0.4f, t => 0.3f + (0.7f * MathF.Pow(MathF.Abs(MathF.Sin((t * 23f) + MathF.Sin(t * 7f))), 6f)), 0.5f);
        a["ocean"] = Noise(8f, r, 0.06f, t => 0.35f + (0.65f * MathF.Pow(MathF.Sin(MathF.PI * ((t % 4f) / 4f)), 2f)), 1.1f);
        a["town"] = Babble(r, 200f, 6f, 0.06f);
        foreach (string key in a.Keys.ToList())
        {
            a[key] = CrossfadeLoop(a[key]);
        }

        return a;
    }

    /// <summary>Makes a buffer loop seamlessly by crossfading its end into its start.</summary>
    private static float[] CrossfadeLoop(float[] b)
    {
        int n = Math.Min(b.Length / 4, Rate / 4);
        for (int i = 0; i < n; i++)
        {
            float g = i / (float)n;
            b[i] = (b[i] * g) + (b[b.Length - n + i] * (1f - g));
        }

        return b[..^n];
    }

    private static float[] Knock(Random r) =>
        Sequence((Noise(0.08f, r, 0.25f, t => MathF.Exp(-t * 50f), 0.8f), 0f), (Tone(180f, 0.1f, t => MathF.Exp(-t * 40f), Sine).Select(v => v * 0.5f).ToArray(), 0f));

    private static float[] Bark(Random r, float pitch)
    {
        float[] b = Buffer(0.22f);
        float phase = 0f;
        float lp = 0f;
        for (int i = 0; i < b.Length; i++)
        {
            float t = i / (float)Rate;
            float hz = pitch * (1f + (0.4f * MathF.Exp(-t * 20f)));
            phase += hz / Rate;
            float raw = (Saw(phase % 1f) * 0.7f) + ((((float)r.NextDouble() * 2f) - 1f) * 0.3f);
            lp += (raw - lp) * 0.25f;
            b[i] = lp * MathF.Exp(-t * 12f) * MathF.Min(1f, t * 200f) * 0.6f;
        }

        return b;
    }

    private static float[] Thunder(Random r)
    {
        float[] b = Noise(4f, r, 0.02f, t => MathF.Min(1f, t * 8f) * MathF.Exp(-t * 0.9f) * (0.6f + (0.4f * MathF.Sin(t * 9f))), 2.5f);
        float[] crack = Noise(0.3f, r, 0.5f, t => MathF.Exp(-t * 12f), 0.6f);
        Mix(b, crack, 0);
        return b;
    }

    private static float[] Siren(Random r, float low, float high, float seconds)
    {
        float[] b = Buffer(seconds);
        float phase = 0f;
        for (int i = 0; i < b.Length; i++)
        {
            float t = i / (float)Rate;
            float hz = low + ((high - low) * (0.5f + (0.5f * MathF.Sin(t * MathF.Tau * 0.8f))));
            phase += hz / Rate;
            b[i] = Triangle(phase % 1f) * 0.18f * MathF.Min(1f, t * 10f) * MathF.Min(1f, (seconds - t) * 5f);
        }

        return b;
    }

    private static float[] RainLoop(Random r)
    {
        float[] b = Noise(8f, r, 0.35f, t => 1f, 0.35f);
        for (int i = 0; i < 260; i++)
        {
            float[] drop = Sweep(1500f + (float)r.NextDouble() * 2500f, 900f, 0.025f, Sine, 0.05f + ((float)r.NextDouble() * 0.06f));
            Mix(b, drop, r.Next(0, b.Length), 1f, wrap: true);
        }

        return b;
    }

    private static float[] BirdsLoop(Random r)
    {
        float[] b = Buffer(10f);
        for (int i = 0; i < 9; i++)
        {
            float start = (float)r.NextDouble() * 9f;
            float baseHz = 2600f + ((float)r.NextDouble() * 1800f);
            int notes = r.Next(2, 6);
            for (int n = 0; n < notes; n++)
            {
                float[] chirp = Sweep(baseHz * (0.9f + ((float)r.NextDouble() * 0.3f)), baseHz * (1.2f + ((float)r.NextDouble() * 0.4f)), 0.06f + ((float)r.NextDouble() * 0.05f), Sine, 0.12f);
                Mix(b, chirp, (int)((start + (n * 0.11f)) * Rate), 1f, wrap: true);
            }
        }

        return b;
    }

    private static float[] CricketsLoop(Random r)
    {
        float[] b = Buffer(8f);
        for (int c = 0; c < 3; c++)
        {
            float hz = 4200f + (c * 350f);
            float rate = 5f + c;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float pulse = MathF.Pow(MathF.Max(0f, MathF.Sin(MathF.Tau * rate * t)), 8f) * (MathF.Sin(MathF.Tau * 0.3f * (t + c)) > -0.3f ? 1f : 0f);
                b[i] += MathF.Sin(MathF.Tau * hz * t) * pulse * 0.05f;
            }
        }

        return b;
    }

    /// <summary>Wordless "babble" speech, used for lines without a recorded voice.</summary>
    public static float[] Babble(Random r, float pitch, float seconds, float gain = 0.3f)
    {
        float[] b = Buffer(seconds);
        float t0 = 0f;
        float[] formant1 = [730f, 270f, 530f, 570f, 440f, 300f];
        float[] formant2 = [1090f, 2290f, 1840f, 840f, 1020f, 870f];
        while (t0 < seconds - 0.12f)
        {
            float length = 0.07f + ((float)r.NextDouble() * 0.09f);
            int vowel = r.Next(formant1.Length);
            float f1 = formant1[vowel];
            float f2 = formant2[vowel];
            float p = pitch * (0.9f + ((float)r.NextDouble() * 0.25f));
            int start = (int)(t0 * Rate);
            float phase = 0f;
            for (int i = 0; i < (int)(length * Rate) && start + i < b.Length; i++)
            {
                float t = i / (float)Rate;
                phase += p * (1f + (0.15f * (1f - (t / length)))) / Rate;
                float pulse = Saw(phase % 1f);
                float env = MathF.Sin(MathF.PI * t / length);
                float voice = (MathF.Sin(MathF.Tau * f1 * t) * 0.6f) + (MathF.Sin(MathF.Tau * f2 * t) * 0.3f);
                b[start + i] += pulse * (0.4f + (0.6f * voice)) * env * gain;
            }

            t0 += length + (r.NextDouble() < 0.2 ? 0.08f : 0.015f);
        }

        return b;
    }

    public static float VoicePitch(MemberId? member) => member switch
    {
        MemberId.Father => 125f,
        MemberId.Mother => 215f,
        MemberId.OlderSister => 290f,
        MemberId.Player => 265f,
        MemberId.YoungerSister => 330f,
        _ => 180f,
    };
}
