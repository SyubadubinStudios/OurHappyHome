using System.Numerics;
using OurHappyHome.Core.Family;

namespace OurHappyHome.Core.Simulation;

public enum GameMode
{
    Cozy,
    Normal,
    Adventure,
}

public enum NoticeKind
{
    Info,
    Good,
    Warning,
    Danger,
    Money,
    Skill,
    Memory,
}

public enum MusicMood
{
    Cozy,
    Explore,
    Celebration,
    Emotional,
    Tense,
    Night,
}

public enum EffectKind
{
    Hearts,
    Sparkles,
    Confetti,
    Fireworks,
    Smoke,
    Steam,
    Flour,
    Splash,
    Dust,
    Leaves,
    Zzz,
    Notes,
    Coins,
    Stars,
}

/// <summary>Base type of everything the simulation tells the presentation layer.</summary>
public abstract record GameEvent;

public sealed record NoticeEvent(string Text, string Icon, NoticeKind Kind) : GameEvent;

/// <summary>A line of dialogue; <see cref="VoiceKey"/> names a recorded voice clip when one exists.</summary>
public sealed record SpeechEvent(MemberId? Speaker, string SpeakerName, string Text, string? VoiceKey = null) : GameEvent;

public sealed record SoundEvent(string Sound, Vector3? Position = null, float Gain = 1f) : GameEvent;

public sealed record MusicEvent(MusicMood Mood) : GameEvent;

public sealed record EffectEvent(EffectKind Kind, Vector3 Position, float Scale = 1f) : GameEvent;

public sealed record ScreenFlashEvent(float Strength) : GameEvent;

public sealed record ShakeEvent(float Strength) : GameEvent;

public sealed record MemoryEvent(FamilyMemory Memory, bool TakePhoto) : GameEvent;

public sealed record SkillUpEvent(MemberId Member, SkillKind Skill, int Level) : GameEvent;

public sealed record ScenarioEvent(string Title, string Phase) : GameEvent;

public sealed record ChapterEvent(int Chapter, string Title, bool Completed) : GameEvent;

/// <summary>The presentation should open a mini-game (cooking, school, lemonade...); time stops until it reports back.</summary>
public sealed record MiniGameRequest(string Kind, string Argument) : GameEvent;

/// <summary>The presentation should open a panel (recipe picker, shop, gift picker, map...).</summary>
public sealed record OpenPanelEvent(string Panel, string Argument = "") : GameEvent;

/// <summary>
/// Queue based event bus: systems publish during the simulation tick, the
/// game drains the queue once per frame on the UI thread. Systems inside the
/// simulation can also subscribe synchronously.
/// </summary>
public sealed class EventBus
{
    private readonly Queue<GameEvent> _queue = new();
    private readonly List<Action<GameEvent>> _subscribers = [];

    public void Publish(GameEvent e)
    {
        _queue.Enqueue(e);
        foreach (Action<GameEvent> subscriber in _subscribers)
        {
            subscriber(e);
        }
    }

    public void Subscribe(Action<GameEvent> handler) => _subscribers.Add(handler);

    public bool TryDequeue(out GameEvent e)
    {
        if (_queue.Count > 0)
        {
            e = _queue.Dequeue();
            return true;
        }

        e = null!;
        return false;
    }

    public void Clear() => _queue.Clear();

    // Convenience helpers.
    public void Notice(string text, string icon = "ℹ", NoticeKind kind = NoticeKind.Info) => Publish(new NoticeEvent(text, icon, kind));

    public void Sound(string sound, Vector3? position = null, float gain = 1f) => Publish(new SoundEvent(sound, position, gain));

    public void Effect(EffectKind kind, Vector2 position, float height = 1.2f, float scale = 1f) =>
        Publish(new EffectEvent(kind, new Vector3(position.X, height, position.Y), scale));
}
