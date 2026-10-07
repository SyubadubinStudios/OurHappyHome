using System.Numerics;
using OurHappyHome.Core.Family;
using OurHappyHome.Core.Simulation;
using OurHappyHome.Core.World;

namespace OurHappyHome.Core;

public sealed partial class GameSession
{
    private bool _flashlight;

    /// <summary>The controlled member carries a lit flashlight (needs one in the inventory, or candles).</summary>
    public bool FlashlightOn
    {
        get => _flashlight;
        set
        {
            if (value && !State.Inventory.Has("flashlight") && !State.Inventory.Has("candle"))
            {
                return;
            }

            if (value != _flashlight)
            {
                Bus.Sound("click");
            }

            _flashlight = value;
        }
    }

    /// <summary>Speeds 1×, 2× and 4× chosen with the HUD buttons.</summary>
    public void SetSpeed(float speed) => Clock.Speed = Math.Clamp(speed, 1f, 4f);

    /// <summary>The objective line shown under the HUD clock: scenario first, then chapter goals, then needs.</summary>
    public (string Icon, string Text) CurrentObjective()
    {
        if (Scenario is { } scenario && scenario.Objectives.FirstOrDefault(o => !o.Done && !o.Optional) is { } objective)
        {
            return (objective.Icon, objective.Text);
        }

        FamilyMember me = Controlled;
        if (me.Stamina.State == StaminaState.MustRecover)
        {
            return ("😮‍💨", Loc.T("Istirahat dulu untuk memulihkan tenaga", "Rest to recover your stamina"));
        }

        (NeedKind need, float urgency) = me.Needs.MostUrgent();
        if (urgency > 0.45f)
        {
            return (Needs.Icon(need), need switch
            {
                NeedKind.Hunger => Loc.T("Kamu lapar. Cari makanan di dapur.", "You're hungry. Find food in the kitchen."),
                NeedKind.Sleep => Loc.T("Kamu mengantuk. Pergi tidur.", "You're sleepy. Go to bed."),
                NeedKind.Hygiene => Loc.T("Waktunya mandi.", "Time for a shower."),
                NeedKind.Fun => Loc.T("Bosan! Ayo bermain.", "Bored! Go and play."),
                NeedKind.Social => Loc.T("Ngobrol atau peluk keluarga.", "Talk to or hug your family."),
                NeedKind.Energy => Loc.T("Duduk santai sebentar.", "Sit down and relax."),
                _ => Loc.T("Jaga kesehatan.", "Look after your health."),
            });
        }

        if (Date.IsSchoolDay && Hour is >= 6.6f and < 12f && State.School.LessonDay != Clock.DayIndex && me.IsChild)
        {
            return ("🏫", Loc.T("Pergi ke sekolah (tekan M untuk peta)", "Go to school (press M for the map)"));
        }

        if (Progression.Chapters.Get(State.Chapter) is { } chapter && chapter.Goals.FirstOrDefault(g => !g.Done(State)) is { } goal)
        {
            return (goal.Icon, goal.Text);
        }

        return ("🏡", Loc.T("Nikmati hari bersama keluarga", "Enjoy the day with your family"));
    }

    /// <summary>All members that are at home and visible, for the family status panel.</summary>
    public IEnumerable<FamilyMember> VisibleMembers => State.Members.Where(m => !m.Away);

    public bool IsIndoors(Vector2 p) => State.House.IsIndoors(p) || Map.InteriorAt(p) is not null;

    /// <summary>Lights in a room are on when it is dark outside, the power is on and someone is around.</summary>
    public bool RoomLit(RoomId room)
    {
        if (!State.House.PowerOn || (Scenario?.DarkRooms.Contains(room) ?? false))
        {
            return false;
        }

        float hour = Hour;
        bool dark = hour < 6.4f || hour > 17.6f || State.Weather.IsStormy;
        if (!dark)
        {
            return false;
        }

        // Late at night only rooms with someone awake keep their lights on.
        if (hour >= 22.5f || hour < 5.5f)
        {
            return State.Members.Any(m => !m.Away && State.House.RoomAt(m.Position) == room && m.Task?.Activity is not (ActivityId.Sleep or ActivityId.Nap));
        }

        return true;
    }
}
