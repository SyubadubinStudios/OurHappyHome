using System.Numerics;
using OurHappyHome.Core;
using OurHappyHome.Core.Cooking;
using OurHappyHome.Core.Family;
using OurHappyHome.Core.Scenarios;
using OurHappyHome.Core.Simulation;
using OurHappyHome.Core.Time;
using OurHappyHome.Core.World;

namespace OurHappyHome.Tests;

public class SimulationTests
{
    private static GameSession NewSession(GameMode mode = GameMode.Normal, ulong seed = 42)
    {
        SaveSystem.Folder = Path.Combine(Path.GetTempPath(), "ohh-tests-" + Guid.NewGuid().ToString("N"));
        return GameSession.NewGame(mode, "Raka", new GameSettings(), seed);
    }

    /// <summary>Runs the simulation for game minutes with a fixed real time step.</summary>
    private static void Run(GameSession session, double gameMinutes, float step = 0.1f)
    {
        double end = session.Now + gameMinutes;
        int guard = 0;
        while (session.Now < end && guard++ < 2_000_000)
        {
            if (session.PendingMiniGame is not null)
            {
                session.CompleteMiniGame(0.7f);
            }

            if (session.ScenarioFailed)
            {
                session.AbandonScenario();
            }

            session.Tick(step);
            while (session.Bus.TryDequeue(out _))
            {
            }
        }
    }

    [Fact]
    public void NewGameHasFiveIndependentMembers()
    {
        GameSession s = NewSession();
        Assert.Equal(5, s.State.Members.Count);
        Assert.Equal(FamilyNames.All, s.State.Members.Select(m => m.Id));
        Assert.All(s.State.Members, m => Assert.False(s.Collision.Blocked(m.Position, GameSession.CharacterRadius * 0.8f)));
    }

    [Fact]
    public void FamilyLivesAutonomouslyThroughTwoDays()
    {
        GameSession s = NewSession(seed: 7);
        Run(s, 2 * 24 * 60);
        Assert.True(s.Clock.DayIndex >= 2);

        // Autonomous members should never starve or collapse (the controlled one waits for the player).
        foreach (FamilyMember m in s.State.Members.Where(m => m.Id != s.State.Controlled))
        {
            Assert.True(m.Needs[NeedKind.Hunger] > 5f, $"{m.Id} hunger {m.Needs[NeedKind.Hunger]}");
            Assert.True(m.Needs[NeedKind.Health] > 30f, $"{m.Id} health {m.Needs[NeedKind.Health]}");
        }

        Assert.True(s.State.Stat(Core.Progression.Stat.FamilyMeals) >= 1, "the family should have eaten together");
        Assert.NotEmpty(s.State.Memories);
    }

    [Theory]
    [InlineData(1UL, GameMode.Cozy)]
    [InlineData(2UL, GameMode.Normal)]
    [InlineData(3UL, GameMode.Adventure)]
    [InlineData(4UL, GameMode.Cozy)]
    [InlineData(5UL, GameMode.Normal)]
    [InlineData(6UL, GameMode.Adventure)]
    public void FamilyStaysHealthyForAWeek(ulong seed, GameMode mode)
    {
        GameSession s = NewSession(mode, seed);
        Run(s, 7 * 24 * 60);
        foreach (FamilyMember m in s.State.Members.Where(m => m.Id != s.State.Controlled && !m.Away))
        {
            Assert.True(m.Needs[NeedKind.Hunger] > 5f, $"{m.Id} hunger {m.Needs[NeedKind.Hunger]} (seed {seed})");
            Assert.True(m.Needs[NeedKind.Sleep] > 5f, $"{m.Id} sleep {m.Needs[NeedKind.Sleep]} (seed {seed})");
        }

        Assert.True(s.State.Stat(Core.Progression.Stat.FamilyMeals) >= 4, $"meals {s.State.Stat(Core.Progression.Stat.FamilyMeals)}");
        Assert.True(s.State.Memories.Count >= 5);
    }

    [Fact]
    public void ControlledMemberDoesNotRunAi()
    {
        GameSession s = NewSession();
        Vector2 start = s.Controlled.Position;
        Run(s, 30);
        Assert.Null(s.Controlled.Task);
        Assert.Equal(start, s.Controlled.Position);
    }

    [Fact]
    public void PlayerMovesAndCollidesWithWalls()
    {
        GameSession s = NewSession();
        s.PlayerMove = new Vector2(0f, -1f); // towards the back wall of the kids' room
        for (int i = 0; i < 200; i++)
        {
            s.Tick(0.05f);
        }

        // The back wall is at z = -6: the player must stop in front of it.
        Assert.True(s.Controlled.Position.Y > -6f + GameSession.CharacterRadius - 0.05f);
    }

    [Fact]
    public void WallsHaveDoorsBetweenBuiltRooms()
    {
        House house = House.CreateStarter();
        Assert.Contains(house.Doors, d => d.Exterior);
        Assert.Contains(house.Doors, d => d.A == RoomId.Hall || d.B == RoomId.Hall);
        Assert.DoesNotContain(house.Doors, d => d.B == RoomId.GirlsBedroom);
        house.Build(RoomId.GirlsBedroom);
        house.Touch();
        Assert.Contains(house.Doors, d => d.B == RoomId.GirlsBedroom || d.A == RoomId.GirlsBedroom);
    }

    [Fact]
    public void NavigationFindsPathFromBedroomToKitchen()
    {
        GameSession s = NewSession();
        List<Vector2>? path = s.FindPath(new Vector2(3f, -3f), new Vector2(3f, 4f));
        Assert.NotNull(path);
        Assert.True(path!.Count >= 2);
    }

    [Fact]
    public void CookingQualityFollowsSkill()
    {
        GameRandom r = new(1);
        Recipe pancakes = Recipes.Get("pancakes");
        int lowGood = 0;
        int highGood = 0;
        for (int i = 0; i < 300; i++)
        {
            lowGood += Recipes.Evaluate(pancakes, 0f, null, false, r) >= CookQuality.Delicious ? 1 : 0;
            highGood += Recipes.Evaluate(pancakes, 9f, null, false, r) >= CookQuality.Delicious ? 1 : 0;
        }

        Assert.True(highGood > lowGood * 2);
        Assert.Equal(CookQuality.Perfect, Recipes.Evaluate(pancakes, 10f, 1f, true, r));
        Assert.Equal(CookQuality.Failed, Recipes.Evaluate(pancakes, 0f, 0f, false, r));
    }

    [Theory]
    [InlineData(100f, StaminaState.Ready)]
    [InlineData(60f, StaminaState.Tired)]
    [InlineData(20f, StaminaState.Exhausted)]
    [InlineData(0f, StaminaState.MustRecover)]
    public void StaminaThresholdsMatchDesign(float value, StaminaState expected)
    {
        Stamina stamina = new() { Value = value };
        Assert.Equal(expected, stamina.State);
    }

    [Fact]
    public void ExhaustedCannotDoDemandingActions()
    {
        Stamina stamina = new() { Value = 15f };
        Assert.False(stamina.TrySpend(10f));
        stamina.Value = 0f;
        stamina.Value = 20f;
        Assert.Equal(StaminaState.MustRecover, stamina.State); // still recovering until 35%
        stamina.Value = 40f;
        Assert.True(stamina.CanDoDemanding);
    }

    [Fact]
    public void SaveAndLoadRoundTrips()
    {
        GameSession s = NewSession();
        Run(s, 300);
        s.State.Wallet.Earn(12345, "test", 0);
        string json = SaveSystem.Serialize(s.State);
        GameState loaded = SaveSystem.Deserialize(json);
        Assert.Equal(s.State.Wallet.Money, loaded.Wallet.Money);
        Assert.Equal(s.State.Clock.TotalMinutes, loaded.Clock.TotalMinutes, 3);
        Assert.Equal(s.State.House.Furniture.Count, loaded.House.Furniture.Count);
        Assert.Equal(s.State.Relationships[MemberId.Player, MemberId.Father], loaded.Relationships[MemberId.Player, MemberId.Father], 3);
        Assert.Equal(s.State.Members[2].Skills[SkillKind.Drawing], loaded.Members[2].Skills[SkillKind.Drawing], 3);
        Assert.Equal(s.State.Memories.Count, loaded.Memories.Count);
    }

    [Theory]
    [InlineData(ScenarioKind.CatVisitor)]
    [InlineData(ScenarioKind.LightBulb)]
    [InlineData(ScenarioKind.FaucetLeak)]
    [InlineData(ScenarioKind.ApplianceBroken)]
    [InlineData(ScenarioKind.MonkeyThief)]
    [InlineData(ScenarioKind.PowerOutage)]
    [InlineData(ScenarioKind.LocalFlood)]
    [InlineData(ScenarioKind.DangerousAnimal)]
    [InlineData(ScenarioKind.SuspiciousStranger)]
    [InlineData(ScenarioKind.Burglary)]
    [InlineData(ScenarioKind.SmallFire)]
    [InlineData(ScenarioKind.GreatStorm)]
    public void EveryScenarioStartsAndTicksWithoutErrors(ScenarioKind kind)
    {
        GameSession s = NewSession(GameMode.Adventure, 99);
        Run(s, 120);
        s.StartScenario(kind);
        Assert.NotNull(s.Scenario);
        Assert.NotEmpty(s.Scenario!.Objectives);
        _ = s.GetInteractions();
        Run(s, 90);
    }

    [Fact]
    public void FailedRescueCanBeRestarted()
    {
        GameSession s = NewSession(GameMode.Adventure, 5);
        Run(s, 60);
        s.StartScenario(ScenarioKind.SmallFire);
        FamilyMember? trapped = s.State.Members.FirstOrDefault(m => m.InDanger);
        Assert.NotNull(trapped);

        // Let the rescue timer run out in real seconds.
        for (int i = 0; i < 4000 && !s.ScenarioFailed; i++)
        {
            s.Tick(0.1f);
        }

        Assert.True(s.ScenarioFailed);
        s.RestartScenario();
        Assert.False(s.ScenarioFailed);
        Assert.NotNull(s.Scenario);
        Assert.Equal(ScenarioKind.SmallFire, s.Scenario!.Kind);
    }

    [Fact]
    public void RescuedMemberBecomesSafe()
    {
        GameSession s = NewSession(GameMode.Normal, 11);
        Run(s, 60);
        s.StartScenario(ScenarioKind.DangerousAnimal);
        FamilyMember? child = s.State.Members.FirstOrDefault(m => m.InDanger);
        Assert.NotNull(child);
        s.Controlled.Position = child!.Position + new Vector2(0.8f, 0f);
        s.Rescue(s.Controlled, child);
        Assert.Equal(SafetyState.Following, child.Safety);

        // Walk the player into the living room; the child follows.
        s.Controlled.Position = new Vector2(-3f, 3f);
        for (int i = 0; i < 200; i++)
        {
            s.Tick(0.1f);
        }

        Assert.NotEqual(SafetyState.Following, child.Safety);
        Assert.False(s.ScenarioFailed);
    }

    [Fact]
    public void CozyModeHasFewerDangerousEvents()
    {
        GameSession cozy = NewSession(GameMode.Cozy);
        GameSession adventure = NewSession(GameMode.Adventure);
        Assert.True(cozy.Director.DangerMultiplier(Rarity.Rare) < adventure.Director.DangerMultiplier(Rarity.Rare) / 10f);
    }

    [Fact]
    public void BuildingARoomCostsMoneyAndAddsWalls()
    {
        GameSession s = NewSession();
        s.State.Wallet.Money = 10_000_000;
        int walls = s.State.House.Walls.Count;
        Assert.True(s.BuildRoom(RoomId.GirlsBedroom));
        Assert.True(s.State.House.Has(RoomId.GirlsBedroom));
        Assert.Equal(7_500_000, s.State.Wallet.Money);
        Assert.NotEqual(walls, s.State.House.Walls.Count);
        Assert.Contains(s.State.House.Furniture, f => f.Room == RoomId.GirlsBedroom);
    }

    [Fact]
    public void FurniturePlacementRejectsOverlaps()
    {
        House house = House.CreateStarter();
        FurnitureDef sofa = FurnitureCatalog.Get("sofa");
        Assert.False(house.CanPlace(sofa, RoomId.LivingRoom, new Vector2(-3.4f, 1.0f), 0));
        Assert.False(house.CanPlace(sofa, RoomId.LivingRoom, new Vector2(-30f, 1.0f), 0));
    }

    [Fact]
    public void CalendarHasDesignDocumentDates()
    {
        GameDate first = GameDate.FromDayIndex(0);
        Assert.Equal((1, 3, Weekday.Monday), (first.Month, first.Day, first.Weekday));
        Assert.Contains(Calendar.EventsOn(GameDate.FromDayIndex(GameDate.ToDayIndex(1, 1, 12))), e => e.Kind == CalendarEventKind.Birthday && e.Member == MemberId.YoungerSister);
        Assert.Contains(Calendar.EventsOn(GameDate.FromDayIndex(GameDate.ToDayIndex(1, 1, 18))), e => e.Kind == CalendarEventKind.Camping);
        Assert.Contains(Calendar.EventsOn(GameDate.FromDayIndex(GameDate.ToDayIndex(1, 1, 24))), e => e.Kind == CalendarEventKind.Festival);
    }

    [Fact]
    public void GiftsFollowLikes()
    {
        GameSession s = NewSession();
        s.State.Inventory.Add("storybook");
        s.State.Inventory.Add("toolset");
        float before = s.State.Relationships[MemberId.Player, MemberId.YoungerSister];
        s.GiveGift(MemberId.YoungerSister, "storybook");
        float loved = s.State.Relationships[MemberId.Player, MemberId.YoungerSister] - before;
        before = s.State.Relationships[MemberId.Player, MemberId.OlderSister];
        s.GiveGift(MemberId.OlderSister, "toolset");
        float meh = s.State.Relationships[MemberId.Player, MemberId.OlderSister] - before;
        Assert.True(loved > meh);
    }

    [Fact]
    public void SchoolQuizzesHaveValidAnswers()
    {
        GameRandom r = new(3);
        foreach (Core.School.Subject subject in Core.School.Lessons.All)
        {
            foreach (Core.School.Question q in Core.School.Lessons.Quiz(subject, 4f, r, 8))
            {
                Assert.InRange(q.Answer, 0, q.Choices.Length - 1);
                Assert.Equal(q.Choices.Length, q.Choices.Distinct().Count());
            }
        }
    }

    [Fact]
    public void WorldPlacesAreReachableAndNotBlocked()
    {
        GameSession s = NewSession();
        foreach (Place place in s.Map.Places)
        {
            Assert.False(s.Collision.Blocked(place.Entrance, GameSession.CharacterRadius), $"{place.Id} entrance is blocked");
        }
    }

    [Fact]
    public void InteriorsAreEnclosedWithFreeSpawnExitAndServices()
    {
        GameSession s = NewSession();
        Assert.Equal(3, s.Map.Interiors.Count);
        foreach (Interior interior in s.Map.Interiors)
        {
            Assert.False(s.Collision.Blocked(interior.Spawn, GameSession.CharacterRadius), $"{interior.Place} spawn is blocked");
            Assert.False(s.Collision.Blocked(interior.Exit, GameSession.CharacterRadius), $"{interior.Place} exit is blocked");
            Assert.True(interior.Area.Contains(interior.Exit), $"{interior.Place} exit is outside the room");
            foreach (Vector2 service in interior.Services)
            {
                Assert.False(s.Collision.Blocked(service, GameSession.CharacterRadius), $"{interior.Place} service point {service} is blocked");
            }

            // Walls all around: just outside each side is solid.
            Rect a = interior.Area;
            Assert.True(s.Collision.Blocked(new Vector2(a.Center.X, a.Z0 - 0.1f), 0.05f));
            Assert.True(s.Collision.Blocked(new Vector2(a.Center.X, a.Z1 + 0.1f), 0.05f));
            Assert.True(s.Collision.Blocked(new Vector2(a.X0 - 0.1f, a.Center.Y), 0.05f));
            Assert.True(s.Collision.Blocked(new Vector2(a.X1 + 0.1f, a.Center.Y), 0.05f));
            Assert.Equal(interior.Place, s.Map.PlaceAt(interior.Spawn));
        }
    }

    [Fact]
    public void RecipesUseKnownIngredientsAndCanBeCooked()
    {
        foreach (Recipe recipe in Recipes.All)
        {
            Assert.All(recipe.Ingredients, i => Assert.True(OurHappyHome.Core.Economy.ItemCatalog.Exists(i.Item), $"{recipe.Id}: unknown {i.Item}"));
            Assert.NotEmpty(recipe.Steps);
        }

        GameSession s = NewSession();
        s.State.Inventory.Add("noodles", 1);
        Assert.True(Recipes.Get("mie-goreng").CanMake(s.State.Inventory));
    }

    [Fact]
    public void CostumesNeedTheItemAndSurviveSaving()
    {
        GameSession s = NewSession();
        Assert.False(s.SetAccessory(MemberId.YoungerSister, "crown"));
        s.State.Inventory.Add("crown", 1);
        Assert.True(s.SetAccessory(MemberId.YoungerSister, "crown"));
        Assert.False(s.SetAccessory(MemberId.YoungerSister, "egg"));
        s.State.Memories.Add(new FamilyMemory { Id = 99, Title = "Test", Frame = "gold", Stickers = ["⭐", "💖"] });

        GameState loaded = SaveSystem.Deserialize(SaveSystem.Serialize(s.State));
        Assert.Equal("crown", loaded.Member(MemberId.YoungerSister).Accessory);
        FamilyMemory memory = loaded.Memories.Single(m => m.Id == 99);
        Assert.Equal("gold", memory.Frame);
        Assert.Equal(["⭐", "💖"], memory.Stickers);

        Assert.True(s.SetAccessory(MemberId.YoungerSister, null));
        Assert.Null(s.State.Member(MemberId.YoungerSister).Accessory);
    }

    [Fact]
    public void FamilyCanEnterAndLeaveTheSupermarket()
    {
        GameSession s = NewSession();
        Run(s, Math.Max(0, (10f - s.Hour) * 60f));
        s.AbandonScenario();
        Place market = s.Map.Get(PlaceId.Supermarket);
        s.Travel(PlaceId.Supermarket, true);
        Assert.Equal(market.Entrance, s.Controlled.Position);

        InteractionOption enter = s.GetInteractions().First(t => t.Key == "place:Supermarket").Options.First(o => o.Id == "enter");
        Assert.True(enter.Enabled, enter.Reason);
        enter.Execute();
        Interior inside = s.CurrentInterior!;
        Assert.Equal(PlaceId.Supermarket, inside.Place);
        Assert.True(s.IsIndoors(s.Controlled.Position));
        Assert.All(s.State.Party, id => Assert.True(inside.Area.Contains(s.State.Member(id).Position)));

        // Walking north ends at the back wall, still inside.
        s.PlayerMove = new Vector2(0f, -1f);
        for (int i = 0; i < 400; i++)
        {
            s.Tick(0.05f);
        }

        s.PlayerMove = Vector2.Zero;
        Assert.True(inside.Area.Contains(s.Controlled.Position));

        // The cashier sells groceries; the exit mat leads back to the street.
        s.Controlled.Position = inside.Services[0];
        Assert.Contains(s.GetInteractions(), t => t.Key.StartsWith("service:Supermarket", StringComparison.Ordinal) && t.Options.Any(o => o.Id == "shop"));
        s.Controlled.Position = inside.Exit;
        s.GetInteractions().First(t => t.Key == "interior-exit").Options[0].Execute();
        Assert.Null(s.CurrentInterior);
        Assert.Equal(market.Entrance, s.Controlled.Position);
    }

    // ------------------------------------------------------------------ v1.2

    /// <summary>Jumps the clock to a date and hour without simulating the time in between.</summary>
    private static void SetTime(GameSession s, int month, int day, float hour)
    {
        int index = GameDate.ToDayIndex(1, month, day);
        s.State.Clock.TotalMinutes = (index * 1440.0) + (hour * 60.0);
        s.State.ScheduleCursor = s.State.Clock.TotalMinutes - 0.5;
        s.Tick(0.05f);
    }

    [Fact]
    public void HolidaysSeasonsAndFestivalsFollowTheCalendar()
    {
        GameDate first = GameDate.FromDayIndex(0);
        Assert.True(first.IsSchoolDay);
        Assert.Equal(Season.Rainy, first.Season);

        GameDate independence = GameDate.FromDayIndex(GameDate.ToDayIndex(1, 8, 17));
        Assert.False(independence.IsWorkDay);
        Assert.False(independence.IsSchoolDay);
        Assert.Equal(Season.Dry, independence.Season);
        Assert.True(Calendar.IsFestivalDay(independence));

        GameDate breakDay = GameDate.FromDayIndex(GameDate.ToDayIndex(1, 7, 1));
        Assert.Equal(breakDay.Weekday is Weekday.Saturday or Weekday.Sunday ? false : true, breakDay.IsWorkDay);
        Assert.False(breakDay.IsSchoolDay);

        // Every month has a festival on its fourth Saturday.
        for (int month = 1; month <= 12; month++)
        {
            Assert.Contains(Enumerable.Range(1, GameDate.DaysInMonth(month)), d => Calendar.IsFestivalDay(GameDate.FromDayIndex(GameDate.ToDayIndex(1, month, d))));
        }

        Assert.Contains(Calendar.EventsOn(GameDate.FromDayIndex(GameDate.ToDayIndex(1, 12, 22))), e => e.Kind == CalendarEventKind.MothersDay);
    }

    [Fact]
    public void NeighboursFollowTheirSchedulesAndDimasVisitsFriends()
    {
        GameSession s = NewSession();
        Npc teacher = s.Npcs.Single(n => n.Id == "teacher");
        Npc dimas = s.Npcs.Single(n => n.Id == "dimas");
        Interior classroom = s.Map.InteriorFor(PlaceId.School)!;

        SetTime(s, 1, 3, 9f); // Monday
        Assert.True(teacher.Present);
        Assert.True(classroom.Area.Contains(teacher.Position));
        Assert.True(classroom.Area.Contains(dimas.Position));

        SetTime(s, 1, 8, 10f); // Saturday: no school, Dimas does not know us yet
        Assert.False(classroom.Area.Contains(teacher.Position));
        Assert.False(dimas.Present);

        s.State.Friendships["dimas"] = 35f;
        SetTime(s, 1, 9, 10f); // Sunday morning: a good friend comes over
        Assert.True(dimas.Present);
        Assert.True(Rooms.Lot.Contains(dimas.Position));
    }

    [Fact]
    public void FestivalStallsOpenOnFestivalDaysAndContestsMakeMemories()
    {
        GameSession s = NewSession();
        SetTime(s, 1, 24, 16f);
        Assert.True(s.FestivalActive);
        s.Travel(PlaceId.Park, true);
        TownFeature stall = s.Map.Features.First(f => f.Kind == FeatureKind.FestivalStall && f.Label == "tug");
        s.Controlled.Position = stall.Area.Center + new Vector2(0f, 1.6f);
        InteractionOption tug = s.GetInteractions().SelectMany(t => t.Options).First(o => o.Id == "tug");
        Assert.True(tug.Enabled, tug.Reason);
        tug.Execute();
        Assert.Equal("tug", s.PendingMiniGame?.Kind);
        int memories = s.State.Memories.Count;
        s.CompleteMiniGame(0.9f);
        Assert.Equal(memories + 1, s.State.Memories.Count);
        Assert.Equal(1, s.State.Stat(Core.Progression.Stat.FestivalGames));

        SetTime(s, 1, 25, 16f);
        Assert.False(s.FestivalActive);
    }

    [Fact]
    public void DrivingIsFasterAndTheAngkotCostsAFare()
    {
        GameSession s = NewSession();
        Assert.False(s.CanDrive); // no garage and car yet
        s.State.House.Furniture.Add(new FurnitureItem { Uid = 9_999, DefId = "car", Room = RoomId.Garage, Position = new Vector2(11.5f, 9f) });
        Assert.True(s.CanDrive);
        float walk = s.TravelMinutes(PlaceId.Beach, TravelMode.Walk);
        float car = s.TravelMinutes(PlaceId.Beach, TravelMode.Car);
        Assert.True(car < walk);

        s.Travel(PlaceId.Mall, false, TravelMode.Car);
        Assert.Contains(MemberId.Father, s.State.Party);

        s.Travel(PlaceId.Home, false);
        long money = s.State.Wallet.Money;
        s.Travel(PlaceId.Park, false, TravelMode.Angkot);
        Assert.Equal(money - GameSession.AngkotFare, s.State.Wallet.Money);
    }

    [Fact]
    public void FamilyCanCampOvernightOnlyBeforeADayOff()
    {
        GameSession s = NewSession();
        s.State.Inventory.Add("tent");
        SetTime(s, 1, 4, 19f); // Tuesday: school tomorrow
        s.Travel(PlaceId.Camping, true);
        Assert.NotNull(s.OvernightBlocked(PlaceId.Camping));

        SetTime(s, 1, 8, 19f); // Saturday evening
        s.Travel(PlaceId.Camping, true);
        Assert.Null(s.OvernightBlocked(PlaceId.Camping));
        Assert.True(s.StayOvernight(PlaceId.Camping));
        Assert.Equal(7f, s.Hour, 1);
        Assert.Equal(Weekday.Sunday, s.Date.Weekday);
        Assert.All(s.State.Members.Where(m => !m.Away), m => Assert.True(m.Needs[NeedKind.Sleep] > 95f));
        Assert.Contains(s.State.Memories, m => m.Tag.StartsWith("overnight:", StringComparison.Ordinal));
    }

    [Fact]
    public void MembersDressForTheRainUnlessThePlayerChoseACostume()
    {
        GameSession s = NewSession();
        s.SetWeather(WeatherKind.Rain);
        FamilyMember dinda = s.State.Member(MemberId.YoungerSister);
        FamilyMember nara = s.State.Member(MemberId.OlderSister);
        s.State.Inventory.Add("crown");
        Assert.True(s.SetAccessory(MemberId.OlderSister, "crown"));
        dinda.Position = nara.Position = new Vector2(-6f, 11f); // front yard
        SetTime(s, 1, 3, 10f + (10f / 60f));
        Assert.Equal("rain-hat", dinda.Accessory);
        Assert.Equal("crown", nara.Accessory);
    }

    // ------------------------------------------------------------------ v1.3

    [Fact]
    public void BeachAndCampsiteAreFullOfLife()
    {
        GameSession s = NewSession();
        Place beach = s.Map.Get(PlaceId.Beach);
        Place camp = s.Map.Get(PlaceId.Camping);
        Rect beachArea = new(-80f, 226f, 180f, 340f);
        Rect campArea = new(-115f, -305f, 65f, -168f);
        Assert.True(s.Map.Features.Count(f => f.Kind is FeatureKind.Prop or FeatureKind.Tree or FeatureKind.Decal && beachArea.Contains(f.Area.Center)) >= 40);
        Assert.True(s.Map.Features.Count(f => f.Kind == FeatureKind.PineTree && campArea.Contains(f.Area.Center)) >= 100);
        Assert.Contains(s.Map.Features, f => f.Kind == FeatureKind.Pond && campArea.Contains(f.Area.Center));

        // Arrival points are free, with no tree right behind the camera.
        foreach (Place place in new[] { beach, camp })
        {
            Assert.False(s.Collision.Blocked(place.Entrance, GameSession.CharacterRadius));
        }

        Assert.DoesNotContain(s.Map.Features, f => f.Kind == FeatureKind.PineTree && Vector2.Distance(f.Area.Center, camp.Entrance) < 7f);

        // People can paddle in the shallows but not swim out to sea.
        Assert.False(s.Collision.Blocked(new Vector2(40f, 303f), GameSession.CharacterRadius));
        Assert.True(s.Collision.Blocked(new Vector2(40f, 312f), GameSession.CharacterRadius));
    }

    [Fact]
    public void VisitorsFillTheBeachButAreNotNeighbours()
    {
        GameSession s = NewSession();
        SetTime(s, 1, 8, 11f);
        s.Travel(PlaceId.Beach, false);
        List<Npc> tourists = [.. s.Npcs.Where(n => n.Ambient && n.Present && n.Position.Y > 226f)];
        Assert.True(tourists.Count >= 4);
        s.Controlled.Position = tourists[0].Position + new Vector2(0.8f, 0f);
        Assert.DoesNotContain(s.GetInteractions(), t => t.Key == $"npc:{tourists[0].Id}");

        SetTime(s, 1, 8, 21f);
        Assert.Contains(s.Npcs, n => n.Ambient && n.Present && n.Model.StartsWith("scout", StringComparison.Ordinal));
        Assert.DoesNotContain(s.Npcs, n => n.Ambient && n.Present && n.Model.StartsWith("tourist", StringComparison.Ordinal));
    }

    // ------------------------------------------------------------------ v1.4

    private static GameSession HouseWithUpperFloor()
    {
        GameSession s = NewSession();
        s.State.House.Build(RoomId.UpperHall);
        s.State.House.Build(RoomId.Attic);
        s.State.House.Build(RoomId.Studio);
        s.Tick(0.05f); // rebuild collision and navigation
        return s;
    }

    [Fact]
    public void UpperFloorComesWithStairsBalconyAndItsOwnRooms()
    {
        GameSession s = HouseWithUpperFloor();
        House house = s.State.House;
        Assert.True(house.Has(RoomId.Balcony));
        Assert.Equal(RoomId.Attic, house.RoomAt(Rooms.Get(RoomId.Attic).Area.Center));
        Assert.Equal(RoomId.Balcony, house.RoomAt(Rooms.Get(RoomId.Balcony).Area.Center));
        Assert.True(house.IsIndoors(Rooms.Get(RoomId.Studio).Area.Center));
        Assert.False(house.IsIndoors(Rooms.Get(RoomId.Balcony).Area.Center));
        Assert.Equal(PlaceId.Home, s.Map.PlaceAt(Rooms.Get(RoomId.Attic).Area.Center));
        Assert.True(Rooms.AtHome(Floors.StairTop));
        Assert.Contains(house.Furniture, f => f.DefId == "telescope" && f.Room == RoomId.Balcony);

        // The stairs and the hole around them are solid; both ends are free.
        Assert.True(s.Collision.Blocked(Floors.Stairs.Center, 0.1f));
        Assert.True(s.Collision.Blocked(Floors.StairHole.Center, 0.1f));
        Assert.False(s.Collision.Blocked(Floors.StairBottom, GameSession.CharacterRadius));
        Assert.False(s.Collision.Blocked(Floors.StairTop, GameSession.CharacterRadius));

        // No falling off the balcony.
        Rect balcony = Rooms.Get(RoomId.Balcony).Area;
        Assert.True(s.Collision.Blocked(new Vector2(balcony.Center.X, balcony.Z1), 0.1f));

        GameState loaded = SaveSystem.Deserialize(SaveSystem.Serialize(s.State));
        Assert.True(loaded.House.Has(RoomId.Attic));
        Assert.Contains(loaded.House.Furniture, f => f.Room == RoomId.Studio);
    }

    [Fact]
    public void FamilyWalksUpAndDownTheStairs()
    {
        GameSession s = HouseWithUpperFloor();
        Vector2 living = new(-3f, 3.4f);
        Vector2 attic = Rooms.Get(RoomId.Attic).Area.Center + new Vector2(0.5f, 1.2f);
        List<Vector2> path = s.FindPath(living, attic)!;
        Assert.Contains(path, p => Floors.IsUpper(p));
        Assert.Contains(path, p => !Floors.IsUpper(p));

        FamilyMember me = s.Controlled;
        me.Position = living;
        s.StartTask(me, ActivityId.Idle, null, -1, target: attic, fromPlayer: true, minutes: 1);
        bool climbed = false;
        for (int i = 0; i < 1200 && Vector2.Distance(me.Position, attic) > 0.3f; i++)
        {
            s.Tick(0.05f);
            climbed |= me.Climb is not null;
        }


        Assert.True(climbed);
        Assert.True(Floors.IsUpper(me.Position));
        Assert.Equal(RoomId.Attic, s.State.House.RoomAt(me.Position));

        // And down again with the stairs interaction.
        me.Position = Floors.StairTop;
        InteractionOption down = s.GetInteractions().First(t => t.Key == "stairs").Options[0];
        down.Execute();
        for (int i = 0; i < 100 && me.Climb is not null; i++)
        {
            s.Tick(0.05f);
        }

        Assert.False(Floors.IsUpper(me.Position));
        Assert.Equal(RoomId.Hall, s.State.House.RoomAt(me.Position));
    }

    [Fact]
    public void FamilyStaysHealthyWithATwoStoreyHouse()
    {
        GameSession s = HouseWithUpperFloor();
        bool someoneWentUpstairs = false;
        for (int hour = 0; hour < 3 * 24; hour++)
        {
            Run(s, 60);
            someoneWentUpstairs |= s.State.Members.Any(m => m.Id != s.State.Controlled && Floors.IsUpper(m.Position));
        }

        Assert.True(someoneWentUpstairs, "nobody used the upper floor");
        foreach (FamilyMember m in s.State.Members.Where(m => m.Id != s.State.Controlled && !m.Away))
        {
            Assert.True(m.Needs[NeedKind.Hunger] > 5f, $"{m.Id} hunger {m.Needs[NeedKind.Hunger]}");
            Assert.True(Rooms.AtHome(m.Position) || m.Away, $"{m.Id} wandered off to {m.Position}");
        }
    }
}
