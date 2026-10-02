using OurHappyHome.Core.Simulation;
using static OurHappyHome.Audio.Synth;

namespace OurHappyHome.Audio;

/// <summary>
/// Composes the soundtrack at start-up: one seamless loop per mood (cozy
/// daily life, exploration, celebration, emotional moments, tense
/// emergencies, night), each with its own tempo, key, chords and band.
/// </summary>
public static class MusicComposer
{
    private sealed record Style(
        float Bpm,
        int Root,
        bool Minor,
        int[] Progression,
        int Bars,
        string Lead,
        string Accompaniment,
        string Drums,
        float MelodyDensity,
        int Seed);

    private static readonly int[] MajorScale = [0, 2, 4, 5, 7, 9, 11];
    private static readonly int[] MinorScale = [0, 2, 3, 5, 7, 8, 10];

    private static Style For(MusicMood mood) => mood switch
    {
        MusicMood.Cozy => new(92, 60, false, [0, 5, 3, 4], 16, "piano", "arp", "shaker", 0.55f, 11),
        MusicMood.Explore => new(112, 67, false, [0, 4, 5, 3], 16, "bell", "strum", "pop", 0.6f, 23),
        MusicMood.Celebration => new(124, 65, false, [0, 3, 4, 0], 16, "lead", "stab", "dance", 0.75f, 37),
        MusicMood.Emotional => new(72, 57, true, [0, 5, 2, 6], 12, "piano", "pad", "none", 0.4f, 41),
        MusicMood.Tense => new(104, 62, true, [0, 0, 5, 4], 12, "none", "ostinato", "tick", 0.0f, 53),
        _ => new(76, 63, false, [0, 3, 5, 4], 12, "bell", "pad", "none", 0.35f, 67),
    };

    public static float[] Compose(MusicMood mood, bool calm = false)
    {
        Style style = For(mood);
        Random r = new(style.Seed);
        float beat = 60f / style.Bpm * (calm && mood == MusicMood.Tense ? 1.25f : 1f);
        float bar = beat * 4f;
        float[] song = Buffer(bar * style.Bars);
        int[] scale = style.Minor ? MinorScale : MajorScale;

        int Degree(int degree, int octave = 0)
        {
            int d = ((degree % 7) + 7) % 7;
            int extraOctave = (int)Math.Floor(degree / 7.0);
            return style.Root + scale[d] + (12 * (octave + extraOctave));
        }

        int[] Chord(int degree) => [Degree(degree), Degree(degree + 2), Degree(degree + 4)];

        int previousNote = 4;
        for (int b = 0; b < style.Bars; b++)
        {
            int degree = style.Progression[b % style.Progression.Length];
            int[] chord = Chord(degree);
            int start = (int)(b * bar * Rate);
            bool lastBar = b == style.Bars - 1;

            // ---- bass
            if (style.Accompaniment != "pad" || mood == MusicMood.Night)
            {
                Mix(song, Bass(chord[0] - 24, beat * 1.8f), start, 0.9f, wrap: true);
                Mix(song, Bass(chord[0] - 24 + (b % 2 == 0 ? 7 : 12), beat * 1.6f), start + (int)(beat * 2 * Rate), 0.7f, wrap: true);
            }

            // ---- accompaniment
            switch (style.Accompaniment)
            {
                case "arp":
                    int[] arp = [chord[0], chord[1], chord[2], chord[1] + 12, chord[2], chord[1], chord[0] + 12, chord[2]];
                    for (int i = 0; i < 8; i++)
                    {
                        Mix(song, Piano(arp[i] - 12, beat * 0.45f, 0.45f), start + (int)(i * beat * 0.5f * Rate), 0.7f, wrap: true);
                    }

                    break;
                case "strum":
                    float[] pattern = [0f, 1f, 1.5f, 2.5f, 3f];
                    foreach (float p in pattern)
                    {
                        for (int s = 0; s < 3; s++)
                        {
                            Mix(song, String(chord[s], beat * 1.2f, r, 0.6f), start + (int)(((p * beat) + (s * 0.012f)) * Rate), 0.55f, wrap: true);
                        }
                    }

                    break;
                case "stab":
                    for (int i = 0; i < 4; i++)
                    {
                        foreach (int n in chord)
                        {
                            Mix(song, Lead(n, beat * 0.25f, 0.3f), start + (int)(((i * beat) + (beat * 0.5f)) * Rate), 0.35f, wrap: true);
                        }
                    }

                    break;
                case "pad":
                    Mix(song, Pad([.. chord.Select(n => (float)n - 12)], bar + 0.3f), start, 0.9f, wrap: true);
                    break;
                case "ostinato":
                    for (int i = 0; i < 8; i++)
                    {
                        int note = chord[0] - 12 + (i % 4 == 3 ? 1 : 0);
                        Mix(song, String(note, beat * 0.4f, r, 0.3f), start + (int)(i * beat * 0.5f * Rate), 0.6f, wrap: true);
                    }

                    Mix(song, Pad([chord[0] - 12f, chord[1] - 12f, chord[0] - 24f], bar + 0.3f, 0.03f), start, calm ? 0.5f : 0.9f, wrap: true);
                    break;
            }

            // ---- drums
            for (int i = 0; i < 8; i++)
            {
                int at = start + (int)(i * beat * 0.5f * Rate);
                switch (style.Drums)
                {
                    case "shaker":
                        Mix(song, Hat(r, 0.06f, 0.12f), at, i % 2 == 0 ? 0.6f : 1f, wrap: true);
                        if (i == 0 || i == 4)
                        {
                            Mix(song, Kick(), at, 0.35f, wrap: true);
                        }

                        break;
                    case "pop":
                        if (i % 4 == 0)
                        {
                            Mix(song, Kick(), at, 0.6f, wrap: true);
                        }

                        if (i % 4 == 2)
                        {
                            Mix(song, Snare(r), at, 0.6f, wrap: true);
                        }

                        Mix(song, Hat(r), at, 0.7f, wrap: true);
                        break;
                    case "dance":
                        if (i % 2 == 0)
                        {
                            Mix(song, Kick(), at, 0.7f, wrap: true);
                        }
                        else
                        {
                            Mix(song, Hat(r, 0.08f, 0.3f), at, 0.9f, wrap: true);
                        }

                        if (i % 4 == 2)
                        {
                            Mix(song, Clap(r), at, 0.8f, wrap: true);
                        }

                        break;
                    case "tick":
                        Mix(song, Hat(r, 0.03f, 0.18f), at, calm ? 0.4f : 0.8f, wrap: true);
                        if (i == 0)
                        {
                            Mix(song, Kick(), at, calm ? 0.3f : 0.6f, wrap: true);
                        }

                        break;
                }
            }

            // ---- melody: chord tones on strong beats, stepwise motion between.
            if (style.Lead == "none" || style.MelodyDensity <= 0f)
            {
                continue;
            }

            int steps = 8;
            for (int i = 0; i < steps; i++)
            {
                bool strong = i % 2 == 0;
                if (r.NextDouble() > (strong ? style.MelodyDensity + 0.25f : style.MelodyDensity * 0.6f))
                {
                    continue;
                }

                int target;
                if (strong)
                {
                    int[] tones = [degree, degree + 2, degree + 4, degree + 7];
                    target = tones.OrderBy(t => Math.Abs(t - previousNote) + (r.NextDouble() * 2)).First();
                }
                else
                {
                    target = previousNote + (r.Next(0, 2) == 0 ? -1 : 1);
                }

                target = Math.Clamp(target, 0, 11);
                previousNote = target;
                float length = beat * (strong && r.NextDouble() < 0.35 ? 1f : 0.5f);
                if (lastBar && i >= 6)
                {
                    target = degree;
                    length = beat;
                }

                int midi = Degree(target, 1);
                float[] note = style.Lead switch
                {
                    "bell" => Bell(midi, length * 2.5f),
                    "lead" => Lead(midi, length * 0.9f),
                    _ => Piano(midi, length * 1.3f, 0.9f),
                };
                Mix(song, note, start + (int)(i * beat * 0.5f * Rate), style.Lead == "lead" ? 0.75f : 0.85f, wrap: true);
            }
        }

        Normalize(song, mood == MusicMood.Tense && calm ? 0.45f : 0.7f);
        return song;
    }
}
