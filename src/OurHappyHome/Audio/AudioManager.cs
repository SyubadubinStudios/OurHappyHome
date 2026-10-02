using System.Collections.Concurrent;
using System.Numerics;
using OurHappyHome.Core;
using OurHappyHome.Core.Family;
using OurHappyHome.Core.Simulation;
using ThreeNet;

namespace OurHappyHome.Audio;

/// <summary>
/// All game audio through ThreeNet's spatial mixer: effects, recorded voice
/// lines (ElevenLabs via Rodin MCP) with a babble fallback, ambience loops
/// that follow weather, time and place, and cross-fading mood music.
/// </summary>
public sealed class AudioManager : IDisposable
{
    private readonly AudioEngine? _engine;
    private readonly Dictionary<string, AudioClip> _effects = [];
    private readonly Dictionary<string, (AudioClip Clip, SoundInstance Instance)> _ambience = [];
    private readonly Dictionary<string, float> _ambienceGain = [];
    private readonly Dictionary<MusicMood, AudioClip> _music = [];
    private readonly ConcurrentQueue<(MusicMood Mood, float[] Samples)> _composed = new();
    private readonly Dictionary<string, AudioClip?> _voices = [];
    private readonly Dictionary<MemberId, AudioClip[]> _babble = [];
    private readonly Random _random = new(3);
    private SoundInstance? _musicNow;
    private SoundInstance? _musicOld;
    private MusicMood? _mood;
    private MusicMood _wantedMood = MusicMood.Cozy;
    private float _musicFade = 1f;
    private SoundInstance? _voice;

    public AudioManager(GameSettings settings)
    {
        Settings = settings;
        try
        {
            _engine = AudioEngine.Open();
        }
        catch (Exception)
        {
            _engine = null;
        }

        if (_engine is null)
        {
            return;
        }

        foreach ((string name, float[] samples) in SoundBank.Effects())
        {
            _effects[name] = _engine.CreateClip(samples, 1, Synth.Rate);
        }

        foreach ((string name, float[] samples) in SoundBank.Ambience())
        {
            AudioClip clip = _engine.CreateClip(samples, 1, Synth.Rate);
            SoundInstance instance = _engine.Play(clip, SoundOptions.Flat(0f, loop: true));
            _ambience[name] = (clip, instance);
            _ambienceGain[name] = 0f;
        }

        Random babble = new(9);
        foreach (MemberId id in FamilyNames.All)
        {
            _babble[id] = [.. Enumerable.Range(0, 4).Select(i => _engine.CreateClip(SoundBank.Babble(babble, SoundBank.VoicePitch(id), 0.9f + (i * 0.25f)), 1, Synth.Rate))];
        }

        // Music takes a few seconds to compose: do it in the background, menu theme first.
        bool calm = settings.ReducedIntensity;
        _ = Task.Run(() =>
        {
            foreach (MusicMood mood in new[] { MusicMood.Cozy, MusicMood.Explore, MusicMood.Night, MusicMood.Tense, MusicMood.Celebration, MusicMood.Emotional })
            {
                _composed.Enqueue((mood, MusicComposer.Compose(mood, calm)));
            }
        });
    }

    public GameSettings Settings { get; }

    public bool Available => _engine is not null;

    public bool HasRecordedVoices => Directory.Exists(VoiceFolder) && Directory.GetFiles(VoiceFolder, "*.mp3").Length > 0;

    private static string VoiceFolder => Path.Combine(AppContext.BaseDirectory, "Assets", "Voice");

    // --------------------------------------------------------------- effects

    public void Play(string name, Vector3? position = null, float gain = 1f, float pitch = 1f)
    {
        if (_engine is null || !_effects.TryGetValue(name, out AudioClip? clip))
        {
            return;
        }

        float volume = gain * Settings.SfxVolume;
        SoundOptions options = position is { } p
            ? SoundOptions.At(p, volume) with { MinDistance = 2f, MaxDistance = 40f }
            : SoundOptions.Flat(volume);
        _engine.Play(clip, options with { Pitch = pitch * (0.97f + ((float)_random.NextDouble() * 0.06f)) });
    }

    public void Click() => Play("click", gain: 0.6f);

    // ---------------------------------------------------------------- voices

    /// <summary>Plays a recorded line when it exists, otherwise expressive babble in the member's voice.</summary>
    public void Speak(MemberId? speaker, string? voiceKey, int textLength)
    {
        if (_engine is null)
        {
            return;
        }

        _voice?.Stop();
        float volume = Settings.VoiceVolume * 1.1f;
        if (voiceKey is not null && LoadVoice(voiceKey) is { } recorded)
        {
            // Child voices are recorded by adult voice actors: lift them a little.
            float pitch = speaker switch
            {
                MemberId.YoungerSister => 1.12f,
                MemberId.OlderSister => 1.06f,
                MemberId.Player => 1.1f,
                _ => 1f,
            };
            _voice = _engine.Play(recorded, SoundOptions.Flat(volume) with { Pitch = pitch });
            return;
        }

        MemberId id = speaker ?? MemberId.Father;
        AudioClip[] clips = _babble[id];
        AudioClip clip = clips[Math.Clamp(textLength / 18, 0, clips.Length - 1)];
        _voice = _engine.Play(clip, SoundOptions.Flat(volume * 0.55f) with { Pitch = speaker is null ? 0.85f : 1f });
    }

    private AudioClip? LoadVoice(string key)
    {
        if (_voices.TryGetValue(key, out AudioClip? clip))
        {
            return clip;
        }

        string path = Path.Combine(VoiceFolder, key + ".mp3");
        clip = null;
        if (_engine is not null && File.Exists(path))
        {
            try
            {
                clip = _engine.LoadClip(path);
            }
            catch (ThreeNetException)
            {
                clip = null;
            }
        }

        _voices[key] = clip;
        return clip;
    }

    // ----------------------------------------------------------------- music

    public void SetMood(MusicMood mood) => _wantedMood = mood;

    // ------------------------------------------------------------- ambience

    /// <summary>Target loudness of each ambience loop (0-1), set from weather, time and place.</summary>
    public void SetAmbience(string name, float gain) => _ambienceGain[name] = gain;

    public void Update(float dt, Scene? scene, Node? listener)
    {
        if (_engine is null)
        {
            return;
        }

        _engine.MasterGain = Settings.MasterVolume;
        while (_composed.TryDequeue(out (MusicMood Mood, float[] Samples) track))
        {
            _music[track.Mood] = _engine.CreateClip(track.Samples, 1, Synth.Rate);
        }

        // Cross-fade the music when the mood changes.
        if (_wantedMood != _mood && _music.TryGetValue(_wantedMood, out AudioClip? next))
        {
            _musicOld?.Stop();
            _musicOld = _musicNow;
            _musicNow = _engine.Play(next, SoundOptions.Flat(0f, loop: true));
            _mood = _wantedMood;
            _musicFade = 0f;
        }

        float musicVolume = Settings.MusicVolume * 0.6f;
        if (_musicFade < 1f)
        {
            _musicFade = MathF.Min(1f, _musicFade + (dt / 2.5f));
            _musicOld?.Update(o => o with { Gain = musicVolume * (1f - _musicFade) });
            if (_musicFade >= 1f)
            {
                _musicOld?.Stop();
                _musicOld = null;
            }
        }

        _musicNow?.Update(o => o with { Gain = musicVolume * _musicFade });

        foreach ((string name, (AudioClip _, SoundInstance instance)) in _ambience)
        {
            float target = _ambienceGain.GetValueOrDefault(name) * Settings.SfxVolume * 0.7f;
            instance.Update(o => o with { Gain = o.Gain + ((target - o.Gain) * MathF.Min(1f, dt * 2f)) });
        }

        if (scene is not null && listener is not null)
        {
            _engine.Update(scene, listener, dt);
        }
    }

    public void StopMusic()
    {
        _musicNow?.Stop();
        _musicOld?.Stop();
        _musicNow = null;
        _musicOld = null;
        _mood = null;
    }

    public void Dispose() => _engine?.Dispose();
}
