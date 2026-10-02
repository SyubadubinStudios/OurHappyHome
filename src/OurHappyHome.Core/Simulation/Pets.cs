using System.Numerics;
using System.Text.Json.Serialization;

namespace OurHappyHome.Core.Simulation;

public enum PetKind
{
    Dog,
    Cat,
    Rabbit,
    Hamster,
    Fish,
}

public enum PetState
{
    Idle,
    Wander,
    Follow,
    Sleep,
    Eat,
    Play,
    Bark,
    Hide,
}

/// <summary>A family pet with needs, personality and trust (design section 19).</summary>
public sealed class Pet
{
    public int Id { get; set; }
    public PetKind Kind { get; set; }
    public string Name { get; set; } = "Brownie";
    public float Hunger { get; set; } = 70f;
    public float Happiness { get; set; } = 90f;
    public float Energy { get; set; } = 50f;
    public float Trust { get; set; } = 80f;
    public int AdoptedDay { get; set; }

    /// <summary>"playful", "lazy", "curious", "shy".</summary>
    public string Personality { get; set; } = "playful";

    public Vector2 Position { get; set; }
    public float Yaw { get; set; }

    [JsonIgnore]
    public PetState State { get; set; } = PetState.Idle;

    [JsonIgnore]
    public Vector2 Target { get; set; }

    [JsonIgnore]
    public float StateTimer { get; set; }

    [JsonIgnore]
    public bool Moving { get; set; }

    [JsonIgnore]
    public float BarkCooldown { get; set; }

    public string Icon => Kind switch
    {
        PetKind.Dog => "🐶",
        PetKind.Cat => "🐱",
        PetKind.Rabbit => "🐰",
        PetKind.Hamster => "🐹",
        _ => "🐠",
    };

    public string KindName => Kind switch
    {
        PetKind.Dog => Loc.T("Anjing", "Dog"),
        PetKind.Cat => Loc.T("Kucing", "Cat"),
        PetKind.Rabbit => Loc.T("Kelinci", "Rabbit"),
        PetKind.Hamster => Loc.T("Hamster", "Hamster"),
        _ => Loc.T("Ikan", "Fish"),
    };

    public static long AdoptionCost(PetKind kind) => kind switch
    {
        PetKind.Dog => 500_000,
        PetKind.Cat => 350_000,
        PetKind.Rabbit => 250_000,
        PetKind.Hamster => 120_000,
        _ => 90_000,
    };

    public void Tick(float minutes)
    {
        Hunger = Math.Clamp(Hunger - (minutes * 0.05f), 0f, 100f);
        float energyRate = State == PetState.Sleep ? 0.25f : State is PetState.Play or PetState.Follow ? -0.12f : -0.03f;
        Energy = Math.Clamp(Energy + (minutes * energyRate), 0f, 100f);
        float mood = (Hunger < 25f ? -0.08f : 0.01f) + (Energy < 15f ? -0.03f : 0f);
        Happiness = Math.Clamp(Happiness + (minutes * mood) - (minutes * 0.01f), 0f, 100f);
    }
}
