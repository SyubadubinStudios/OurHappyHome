namespace OurHappyHome.Core.Family;

public enum MemberId
{
    Father,
    Mother,
    OlderSister,
    Player,
    YoungerSister,
}

public static class FamilyNames
{
    public static readonly MemberId[] All = [MemberId.Father, MemberId.Mother, MemberId.OlderSister, MemberId.Player, MemberId.YoungerSister];

    public static readonly MemberId[] Children = [MemberId.OlderSister, MemberId.Player, MemberId.YoungerSister];

    /// <summary>The boy's name; the player can rename him at the start of a new game.</summary>
    public static string PlayerName { get; set; } = "Raka";

    public static string Short(MemberId id) => id switch
    {
        MemberId.Father => Loc.T("Ayah", "Dad"),
        MemberId.Mother => Loc.T("Ibu", "Mom"),
        MemberId.OlderSister => Loc.T("Kak Nara", "Nara"),
        MemberId.Player => PlayerName,
        _ => Loc.T("Dinda", "Dinda"),
    };

    public static string Role(MemberId id) => id switch
    {
        MemberId.Father => Loc.T("Ayah · 25 tahun", "Father · 25"),
        MemberId.Mother => Loc.T("Ibu · 24 tahun", "Mother · 24"),
        MemberId.OlderSister => Loc.T("Kakak · 11 tahun", "Older Sister · 11"),
        MemberId.Player => Loc.T("Kamu · 10 tahun", "You · 10"),
        _ => Loc.T("Adik · 8 tahun", "Younger Sister · 8"),
    };

    public static int Age(MemberId id) => id switch
    {
        MemberId.Father => 25,
        MemberId.Mother => 24,
        MemberId.OlderSister => 11,
        MemberId.Player => 10,
        _ => 8,
    };

    public static bool IsChild(MemberId id) => id is MemberId.OlderSister or MemberId.Player or MemberId.YoungerSister;

    public static bool IsAdult(MemberId id) => !IsChild(id);

    /// <summary>Asset file name of the rigged model.</summary>
    public static string ModelName(MemberId id) => id switch
    {
        MemberId.Father => "father",
        MemberId.Mother => "mother",
        MemberId.OlderSister => "older-sister",
        MemberId.Player => "boy",
        _ => "younger-sister",
    };

    /// <summary>Accent colour (hex) used by the UI for this member.</summary>
    public static string Accent(MemberId id) => id switch
    {
        MemberId.Father => "#4F7CCF",
        MemberId.Mother => "#2FA58C",
        MemberId.OlderSister => "#8E7CE0",
        MemberId.Player => "#D2463F",
        _ => "#E58A3A",
    };
}
