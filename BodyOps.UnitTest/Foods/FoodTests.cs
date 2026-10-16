using System.Text.Json;
using BodyOps.Library.Enums;
using BodyOps.Library.Services;
using BodyOps.WebApi.Features.Days.Models.DTOs;
using BodyOps.WebApi.Features.Days.Models.Entities;
using BodyOps.WebApi.Features.Days.Services;
using BodyOps.WebApi.Features.Foods.Models.DTOs;
using BodyOps.WebApi.Features.Foods.Models.Entities;
using BodyOps.WebApi.Features.Foods.Services;
using BodyOps.WebApi.Features.Plans.Models.Entities;

namespace BodyOps.UnitTest.Foods;

public sealed class FoodTests
{
    private static Food Oats => new() { Name = "Oats", NameKey = "oats", Basis = FoodBasis.Per100g, Kcal = 372, ProteinG = 13.5m, CarbsG = 59, FatG = 7 };

    private static Food Banana => new() { Name = "Banana", NameKey = "banana", Basis = FoodBasis.Per100g, PieceGrams = 118, Kcal = 89, ProteinG = 1.1m };

    private static Food Wrap => new() { Name = "Chicken wrap", NameKey = "chicken wrap", Basis = FoodBasis.PerPiece, Kcal = 610, ProteinG = 42 };

    [Fact]
    public void For_Per100g_InGrams_AndInPiecesThroughThePieceWeight()
    {
        Assert.Equal(new Macros(297.6m, 10.8m, 47.2m, 5.6m), FoodMath.For(Oats, 80, FoodUnit.Grams));
        var banana = FoodMath.For(Banana, 1, FoodUnit.Pieces)!.Value;
        Assert.Equal(105.02m, banana.Kcal);
        Assert.Null(banana.CarbsG);
    }

    [Fact]
    public void For_PerPiece_InPieces_AndGramsOnlyWithAPieceWeight()
    {
        Assert.Equal(1220m, FoodMath.For(Wrap, 2, FoodUnit.Pieces)!.Value.Kcal);
        Assert.Null(FoodMath.For(Wrap, 200, FoodUnit.Grams));
        Assert.Null(FoodMath.For(Oats, 2, FoodUnit.Pieces));
        Assert.Null(FoodMath.For(null, 100, FoodUnit.Grams));
    }

    [Fact]
    public void Sum_AddsWhatIsKnown_AndCountsTheRest()
    {
        var total = FoodMath.Sum([FoodMath.For(Oats, 80, FoodUnit.Grams), FoodMath.For(Banana, 1, FoodUnit.Pieces), null]);
        Assert.Equal(402.6m, total.Kcal);
        Assert.Equal(12.1m, total.ProteinG);
        Assert.Equal(47.2m, total.CarbsG);
        Assert.Equal(1, total.Unknown);
    }

    [Fact]
    public void ParseFoodsLine_ReadsBackTheLoggedDaysLine()
    {
        var line = WeekBuilder.FoodsLine(
        [
            new PlanFood { Name = "Oats 2.0", Amount = 80, Unit = FoodUnit.Grams, SortOrder = 0 },
            new PlanFood { Name = "Banana", Amount = 1.5m, Unit = FoodUnit.Pieces, SortOrder = 1 },
        ]);

        Assert.Equal([("Oats 2.0", 80m, FoodUnit.Grams), ("Banana", 1.5m, FoodUnit.Pieces)], FoodMath.ParseFoodsLine(line));
        Assert.Empty(FoodMath.ParseFoodsLine("Something odd"));
    }

    [Theory]
    [InlineData("Pão  de Centeio", "pao de centeio")]
    [InlineData("  AÇAÍ ", "acai")]
    public void SearchKey_FoldsCaseAndAccents(string text, string key) => Assert.Equal(key, FoodMath.SearchKey(text));

    [Fact]
    public void MiloList_FindsByPortugueseOrEnglish_AndNamesItInTheQuerysLanguage()
    {
        var frango = MiloFoodList.Search("frango peito");
        Assert.Contains(frango, food => food.Id == "u05062");
        Assert.Equal("Peito de frango cru", MiloFoodList.Hit(MiloFoodList.Find("u05062")!, "frango").Name);
        Assert.Equal("Chicken breast, raw", MiloFoodList.Hit(MiloFoodList.Find("u05062")!, "chicken").Name);
        Assert.Equal("Aveia", MiloFoodList.Search("aveia")[0].Pt);
        Assert.Empty(MiloFoodList.Search(" "));
        Assert.Equal(MiloFoodData.All.Count, MiloFoodData.All.Select(food => food.Id).Distinct().Count());
    }

    [Fact]
    public async Task Save_UpsertsByName_RejectsATakenName_AndBadValues()
    {
        var repository = new FakeFoodRepository();
        var service = new FoodService(repository, new FakePackagedSource(), TestSupport.Globalization);

        var first = (await service.SaveAsync(1, null, new FoodSaveDTO { Name = " Oats ", Kcal = 372, ProteinG = 13.54m })).Result!;
        var again = (await service.SaveAsync(1, null, new FoodSaveDTO { Name = "oats", Kcal = 380, ProteinG = 13 })).Result!;
        Assert.Equal(first.Id, again.Id);
        Assert.Single(repository.Foods);
        Assert.Equal(380, repository.Foods[0].Kcal);

        var whey = (await service.SaveAsync(1, null, new FoodSaveDTO { Name = "Whey", Basis = FoodBasis.PerPiece, PieceGrams = 30, Kcal = 117, ProteinG = 23.4m })).Result!;
        var taken = await service.SaveAsync(1, whey.Id, new FoodSaveDTO { Name = "OATS", Kcal = 1 });
        Assert.Equal(ServiceApiResponseStatus.ValidationProblems, taken.Status);
        Assert.Equal(ServiceApiResponseStatus.ValidationProblems, (await service.SaveAsync(1, null, new FoodSaveDTO { Name = "X", ProteinG = 2000 })).Status);
        Assert.Equal(ServiceApiResponseStatus.ValidationProblems, (await service.SaveAsync(1, null, new FoodSaveDTO { Name = "" })).Status);
        Assert.Equal(ServiceApiResponseStatus.NotFound, (await service.SaveAsync(1, 999, new FoodSaveDTO { Name = "Y" })).Status);
        Assert.Equal(ServiceApiResponseStatus.Success, (await service.DeleteAsync(1, whey.Id)).Status);
        Assert.Equal(ServiceApiResponseStatus.NotFound, (await service.DeleteAsync(1, whey.Id)).Status);
    }

    [Fact]
    public async Task Search_MineFirst_ThenMiloListWithoutWhatIsAlreadyMine()
    {
        var repository = new FakeFoodRepository();
        repository.Foods.Add(new Food { FoodID = 1, Name = "Peito de frango cru", NameKey = "peito de frango cru", Kcal = 120, ProteinG = 22.5m, Source = FoodSource.Milo, SourceRef = "u05062" });
        var service = new FoodService(repository, new FakePackagedSource(), TestSupport.Globalization);

        var result = (await service.SearchAsync(1, "frango")).Result!;
        Assert.Equal("Peito de frango cru", Assert.Single(result.Mine).Name);
        Assert.DoesNotContain(result.Milo, hit => hit.SourceRef == "u05062");
        Assert.Contains(result.Milo, hit => hit.SourceRef == "u05747");
    }

    [Fact]
    public async Task Packaged_AndBarcode_MapTheSourcesAnswers()
    {
        var source = new FakePackagedSource();
        source.Products.Add(new FoodHitDTO { Source = FoodSource.OpenFoodFacts, SourceRef = "5601234567890", Name = "Whey vanilla", Kcal = 390, ProteinG = 78 });
        var service = new FoodService(new FakeFoodRepository(), source, TestSupport.Globalization);

        Assert.Single((await service.PackagedAsync("whey")).Result!);
        Assert.Equal("Whey vanilla", (await service.BarcodeAsync("5601234567890")).Result!.Name);
        Assert.Equal(ServiceApiResponseStatus.NotFound, (await service.BarcodeAsync("5600000000000")).Status);
        Assert.Equal(ServiceApiResponseStatus.ValidationProblems, (await service.BarcodeAsync("12ab")).Status);
        source.Status = PackagedStatus.SlowDown;
        Assert.Equal(ServiceApiResponseStatus.Unavailable, (await service.PackagedAsync("whey")).Status);
        source.Status = PackagedStatus.Unavailable;
        Assert.Equal(ServiceApiResponseStatus.Unavailable, (await service.BarcodeAsync("5601234567890")).Status);
    }

    [Fact]
    public void OpenFoodFacts_ReadsAProduct_KcalFromKjWhenNeeded()
    {
        using var json = JsonDocument.Parse("""
            {"code":"5601234567890","product_name":"Greek yogurt","product_name_pt":"Iogurte grego","brands":"Store, Other","quantity":"4 x 125 g",
             "serving_quantity":"125","nutriments":{"energy_100g":397,"proteins_100g":"10","carbohydrates_100g":4.1,"fat_100g":5}}
            """);
        var hit = OpenFoodFactsSource.Read(json.RootElement)!;
        Assert.Equal("Iogurte grego", hit.Name);
        Assert.Equal("Store · 4 x 125 g", hit.Detail);
        Assert.Equal(94.9m, hit.Kcal);
        Assert.Equal(10m, hit.ProteinG);
        Assert.Equal(125m, hit.PieceGrams);

        using var empty = JsonDocument.Parse("""{"product_name":"Water","nutriments":{}}""");
        Assert.Null(OpenFoodFactsSource.Read(empty.RootElement));
    }

    [Fact]
    public void Week_MealsGetTheirMacros_AndTheDayAddsPlannedEatenAndLeft()
    {
        var plan = new PlanData(
            [
                new PlanMeal { PlanMealID = 1, Weekday = 2, Name = "Breakfast", SortOrder = 0 },
                new PlanMeal { PlanMealID = 2, Weekday = 2, Name = "Lunch", SortOrder = 1 },
                new PlanMeal { PlanMealID = 3, Weekday = 2, Name = "Dinner", SortOrder = 2 },
            ],
            [
                new PlanFood { PlanFoodID = 1, PlanMealID = 1, Name = "Oats", Amount = 80, Unit = FoodUnit.Grams },
                new PlanFood { PlanFoodID = 2, PlanMealID = 1, Name = "Banana", Amount = 1, Unit = FoodUnit.Pieces, SortOrder = 1 },
                new PlanFood { PlanFoodID = 3, PlanMealID = 2, Name = "OATS", Amount = 100, Unit = FoodUnit.Grams },
                new PlanFood { PlanFoodID = 4, PlanMealID = 3, Name = "Mystery stew", Amount = 300, Unit = FoodUnit.Grams },
            ],
            []);
        var logged = new DayRange(
            [new DayRow { DayID = 5, DayDate = TestSupport.Tuesday.ToDateTime(TimeOnly.MinValue) }],
            [
                new DayMealRow { DayID = 5, PlanMealID = 1, Name = "Breakfast", Foods = "Oats 80 g · Banana 1 pc", Eaten = true, SortOrder = 0 },
                new DayMealRow { DayID = 5, PlanMealID = 2, Name = "Lunch", Foods = "OATS 100 g", Eaten = true, SortOrder = 1, SwapName = "Chicken wrap", SwapKcal = 610, SwapProteinG = 42 },
                new DayMealRow { DayID = 5, PlanMealID = 3, Name = "Dinner", Foods = "Mystery stew 300 g", Eaten = false, SortOrder = 2 },
            ],
            [],
            []);

        var week = WeekBuilder.Week(TestSupport.Monday, TestSupport.Tuesday, plan, logged, [], null, [Oats, Banana]);

        var tuesday = week.Days[1];
        Assert.Equal(402.6m, tuesday.Meals[0].Macros!.Kcal);
        Assert.Equal("Chicken wrap", tuesday.Meals[1].Swap!.Name);
        Assert.Null(tuesday.Meals[2].Macros);
        Assert.Equal(402.6m + 372m, tuesday.Food!.Planned.Kcal);
        Assert.Equal(402.6m + 610m, tuesday.Food.Eaten.Kcal);
        Assert.Equal(12.1m + 42m, tuesday.Food.Eaten.ProteinG);
        Assert.Null(tuesday.Food.Left.Kcal);

        // The unlogged Monday has no food, so no totals.
        Assert.Null(week.Days[0].Food);
    }

    [Fact]
    public async Task Meal_EatenSomethingElse_IsKept_AndUntickingClearsIt()
    {
        var plan = new FakePlanRepository();
        plan.Meals.Add(new PlanMeal { PlanMealID = 1, Weekday = 2, Name = "Lunch" });
        var days = new FakeDayRepository(plan);
        var service = new DayService(days, plan, new FakeWeightRepository(), new FakeClock(TestSupport.Tuesday), TestSupport.Globalization, new FakeFoodRepository());

        var week = (await service.SetMealEatenAsync(1, TestSupport.Tuesday, 1, new MealEatenDTO { Eaten = true, SwapName = " Chicken  wrap ", SwapKcal = 610, SwapProteinG = 42 })).Result!;
        var lunch = week.Days[1].Meals[0];
        Assert.True(lunch.Eaten);
        Assert.Equal(("Chicken wrap", 610m, 42m), (lunch.Swap!.Name, lunch.Swap.Kcal, lunch.Swap.ProteinG));

        week = (await service.SetMealEatenAsync(1, TestSupport.Tuesday, 1, new MealEatenDTO { Eaten = false, SwapName = "ignored" })).Result!;
        Assert.Null(week.Days[1].Meals[0].Swap);

        var bad = await service.SetMealEatenAsync(1, TestSupport.Tuesday, 1, new MealEatenDTO { Eaten = true, SwapName = "Pizza", SwapKcal = -1 });
        Assert.Equal(ServiceApiResponseStatus.ValidationProblems, bad.Status);
    }
}
