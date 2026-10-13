using System.Text.Json;
using BodyOps.Library.Enums;
using BodyOps.WebApi.Extensions.Authentication;
using BodyOps.WebApi.Extensions.Globalization;
using BodyOps.WebApi.Extensions.Time;
using BodyOps.WebApi.Features;
using BodyOps.WebApi.Features.Accounts.Models;
using BodyOps.WebApi.Features.Accounts.Models.Entities;
using BodyOps.WebApi.Features.Accounts.Repositories;
using BodyOps.WebApi.Features.Days.Models.Entities;
using BodyOps.WebApi.Features.Days.Repositories;
using BodyOps.WebApi.Features.Plans.Models.Entities;
using BodyOps.WebApi.Features.Plans.Repositories;
using BodyOps.WebApi.Features.Settings.Models.Entities;
using BodyOps.WebApi.Features.Settings.Repositories;
using BodyOps.WebApi.Features.Weights.Models.Entities;
using BodyOps.WebApi.Features.Weights.Repositories;
using Microsoft.Extensions.Options;

namespace BodyOps.UnitTest;

/// <summary>Shared helpers for the unit tests.</summary>
internal static class TestSupport
{
    public static readonly IGlobalizationHelper Globalization = new GlobalizationHelper();

    public static IOptions<BodyOpsOptions> Options(Action<BodyOpsOptions>? configure = null)
    {
        var options = new BodyOpsOptions();
        configure?.Invoke(options);
        return Microsoft.Extensions.Options.Options.Create(options);
    }

    /// <summary>A throw-away folder, deleted on dispose.</summary>
    public static TempFolder NewFolder() => new();

    /// <summary>Tuesday 13 October 2026, the day BodyOps was rebuilt.</summary>
    public static readonly DateOnly Tuesday = new(2026, 10, 13);

    public static DateOnly Monday => Tuesday.AddDays(-1);
}

internal sealed class TempFolder : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "bodyops-tests", Guid.NewGuid().ToString("N"));

    public TempFolder() => Directory.CreateDirectory(Path);

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}

/// <summary>A clock stopped on a given day.</summary>
internal sealed class FakeClock(DateOnly today) : IAppClock
{
    public DateOnly Today { get; set; } = today;
}

/// <summary>Accounts in memory, with the same rules as the stored procedures.</summary>
internal sealed class FakeUserRepository : IUserRepository
{
    public List<UserAccount> Users { get; } = [];

    public Dictionary<long, (int Exercises, int Meals)> PlanCounts { get; } = [];

    public List<long> Cleared { get; } = [];

    public Task<int> CountAsync() => Task.FromResult(Users.Count);

    public Task<UserAccount?> GetAsync(long userId) => Task.FromResult(Users.FirstOrDefault(user => user.UserID == userId));

    public Task<UserAccount?> GetByNameAsync(string username) =>
        Task.FromResult(Users.FirstOrDefault(user => string.Equals(user.Username, username, StringComparison.OrdinalIgnoreCase)));

    public Task<List<UserAccountListItem>> ListAsync() => Task.FromResult(Users.Select(user => new UserAccountListItem
    {
        UserID = user.UserID,
        UserUniqueID = user.UserUniqueID,
        Username = user.Username,
        IsAdmin = user.IsAdmin,
        CreatedAt = user.CreatedAt,
        LastLoginAt = user.LastLoginAt,
        PlannedExercises = PlanCounts.GetValueOrDefault(user.UserID).Exercises,
        PlannedMeals = PlanCounts.GetValueOrDefault(user.UserID).Meals,
    }).ToList());

    public Task<UserCreateResult> CreateAsync(string username, string passwordHash, string securityStamp, bool allowRegistration)
    {
        if (Users.Count > 0 && !allowRegistration)
        {
            return Task.FromResult(new UserCreateResult(UserCreateOutcome.RegistrationClosed, null));
        }

        if (Users.Any(user => string.Equals(user.Username, username, StringComparison.OrdinalIgnoreCase)))
        {
            return Task.FromResult(new UserCreateResult(UserCreateOutcome.UsernameTaken, null));
        }

        var created = new UserAccount
        {
            UserID = Users.Count == 0 ? 1 : Users.Max(user => user.UserID) + 1,
            UserUniqueID = Guid.NewGuid(),
            Username = username,
            PasswordHash = passwordHash,
            IsAdmin = Users.Count == 0,
            SecurityStamp = securityStamp,
            CreatedAt = DateTime.UtcNow,
        };
        Users.Add(created);
        return Task.FromResult(new UserCreateResult(UserCreateOutcome.Created, created));
    }

    public Task<UserAccount?> SetPasswordAsync(long userId, string passwordHash, string securityStamp)
    {
        var user = Users.FirstOrDefault(candidate => candidate.UserID == userId);
        if (user is not null)
        {
            user.PasswordHash = passwordHash;
            user.SecurityStamp = securityStamp;
        }

        return Task.FromResult(user);
    }

    public Task TouchLoginAsync(long userId)
    {
        var user = Users.FirstOrDefault(candidate => candidate.UserID == userId);
        if (user is not null)
        {
            user.LastLoginAt = DateTime.UtcNow;
        }

        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(long userId)
    {
        var deleted = Users.RemoveAll(user => user.UserID == userId) > 0;
        if (Users.Count > 0 && !Users.Any(user => user.IsAdmin))
        {
            Users.MinBy(user => user.UserID)!.IsAdmin = true;
        }

        return Task.FromResult(deleted);
    }

    public Task ClearDataAsync(long userId)
    {
        Cleared.Add(userId);
        return Task.CompletedTask;
    }
}

/// <summary>Records sign-ins and sign-outs instead of writing cookies.</summary>
internal sealed class FakeUserSession : IUserSession
{
    public UserAccount? SignedIn { get; private set; }

    public bool SignedOut { get; private set; }

    public Task SignInAsync(UserAccount user, bool remember)
    {
        SignedIn = user;
        SignedOut = false;
        return Task.CompletedTask;
    }

    public Task SignOutAsync()
    {
        SignedIn = null;
        SignedOut = true;
        return Task.CompletedTask;
    }
}

/// <summary>The plan in memory, with the id rules of dbo.PlanMealsSave / dbo.PlanExercisesSave (logged days are not modelled).</summary>
internal sealed class FakePlanRepository : IPlanRepository
{
    private long _nextId = 100;

    public List<PlanMeal> Meals { get; } = [];

    public List<PlanFood> Foods { get; } = [];

    public List<PlanExercise> Exercises { get; } = [];

    public List<DateOnly> SyncDates { get; } = [];

    public List<PlanDayName> Names { get; } = [];

    public Task<PlanData> GetAsync(long userId, int? weekday = null) => Task.FromResult(new PlanData(
        Meals.Where(meal => weekday is null || meal.Weekday == weekday).OrderBy(meal => meal.Weekday).ThenBy(meal => meal.SortOrder).ToList(),
        Foods.Where(food => Meals.Any(meal => meal.PlanMealID == food.PlanMealID && (weekday is null || meal.Weekday == weekday))).ToList(),
        Exercises.Where(exercise => weekday is null || exercise.Weekday == weekday).OrderBy(exercise => exercise.Weekday).ThenBy(exercise => exercise.SortOrder).ToList())
    {
        Names = Names.Where(name => weekday is null || name.Weekday == weekday).OrderBy(name => name.Weekday).ToList(),
    });

    public Task SaveDayNameAsync(long userId, int weekday, string name)
    {
        Names.RemoveAll(existing => existing.Weekday == weekday);
        if (name.Length > 0)
        {
            Names.Add(new PlanDayName { Weekday = weekday, Name = name });
        }

        return Task.CompletedTask;
    }

    public Task<PlanData> SaveMealsAsync(long userId, int weekday, string meals, DateOnly syncFrom)
    {
        SyncDates.Add(syncFrom);
        using var document = JsonDocument.Parse(meals);
        var keep = new List<PlanMeal>();
        var order = 0;
        foreach (var item in document.RootElement.EnumerateArray())
        {
            var id = item.GetProperty("id").ValueKind == JsonValueKind.Number ? item.GetProperty("id").GetInt64() : (long?)null;
            var meal = Meals.FirstOrDefault(candidate => candidate.PlanMealID == id && candidate.Weekday == weekday)
                ?? new PlanMeal { PlanMealID = ++_nextId, Weekday = weekday };
            meal.Name = item.GetProperty("name").GetString()!;
            meal.MealTime = item.TryGetProperty("time", out var time) && time.ValueKind == JsonValueKind.String ? TimeSpan.Parse(time.GetString()!, System.Globalization.CultureInfo.InvariantCulture) : null;
            meal.SortOrder = order++;
            keep.Add(meal);

            Foods.RemoveAll(food => food.PlanMealID == meal.PlanMealID);
            var foodOrder = 0;
            foreach (var food in item.GetProperty("foods").EnumerateArray())
            {
                Foods.Add(new PlanFood
                {
                    PlanFoodID = ++_nextId,
                    PlanMealID = meal.PlanMealID,
                    Name = food.GetProperty("name").GetString()!,
                    Amount = food.GetProperty("amount").GetDecimal(),
                    Unit = (FoodUnit)food.GetProperty("unit").GetInt32(),
                    SortOrder = foodOrder++,
                });
            }
        }

        foreach (var removed in Meals.Where(meal => meal.Weekday == weekday && !keep.Contains(meal)).ToList())
        {
            Meals.Remove(removed);
            Foods.RemoveAll(food => food.PlanMealID == removed.PlanMealID);
        }

        Meals.AddRange(keep.Where(meal => !Meals.Contains(meal)));
        return Task.FromResult(new PlanData(keep, Foods.Where(food => keep.Any(meal => meal.PlanMealID == food.PlanMealID)).ToList(), []));
    }

    public Task<List<PlanExercise>> SaveExercisesAsync(long userId, int weekday, string exercises, DateOnly syncFrom)
    {
        SyncDates.Add(syncFrom);
        using var document = JsonDocument.Parse(exercises);
        var keep = new List<PlanExercise>();
        var order = 0;
        foreach (var item in document.RootElement.EnumerateArray())
        {
            var id = item.GetProperty("id").ValueKind == JsonValueKind.Number ? item.GetProperty("id").GetInt64() : (long?)null;
            var exercise = Exercises.FirstOrDefault(candidate => candidate.PlanExerciseID == id && candidate.Weekday == weekday)
                ?? new PlanExercise { PlanExerciseID = ++_nextId, Weekday = weekday };
            exercise.ExerciseType = (ExerciseType)item.GetProperty("type").GetInt32();
            exercise.Name = item.GetProperty("name").GetString()!;
            exercise.Detail = item.GetProperty("detail").GetString()!;
            exercise.Notes = item.GetProperty("notes").GetString()!;
            exercise.Muscles = item.GetProperty("muscles").ValueKind == JsonValueKind.String ? item.GetProperty("muscles").GetString() : null;
            exercise.SortOrder = order++;
            keep.Add(exercise);
        }

        Exercises.RemoveAll(exercise => exercise.Weekday == weekday && !keep.Contains(exercise));
        Exercises.AddRange(keep.Where(exercise => !Exercises.Contains(exercise)));
        return Task.FromResult(keep);
    }

    public Task CopyDayAsync(long userId, int fromWeekday, IReadOnlyCollection<int> toWeekdays, PlanPart part, DateOnly syncFrom)
    {
        SyncDates.Add(syncFrom);
        foreach (var target in toWeekdays)
        {
            if (part == PlanPart.Meals)
            {
                foreach (var meal in Meals.Where(meal => meal.Weekday == target).ToList())
                {
                    Meals.Remove(meal);
                    Foods.RemoveAll(food => food.PlanMealID == meal.PlanMealID);
                }

                foreach (var meal in Meals.Where(meal => meal.Weekday == fromWeekday).ToList())
                {
                    var copy = new PlanMeal { PlanMealID = ++_nextId, Weekday = target, Name = meal.Name, SortOrder = meal.SortOrder, MealTime = meal.MealTime };
                    Meals.Add(copy);
                    Foods.AddRange(Foods.Where(food => food.PlanMealID == meal.PlanMealID).ToList().Select(food => new PlanFood
                    {
                        PlanFoodID = ++_nextId,
                        PlanMealID = copy.PlanMealID,
                        Name = food.Name,
                        Amount = food.Amount,
                        Unit = food.Unit,
                        SortOrder = food.SortOrder,
                    }));
                }
            }
            else
            {
                Exercises.RemoveAll(exercise => exercise.Weekday == target);
                Names.RemoveAll(name => name.Weekday == target);
                Names.AddRange(Names.Where(name => name.Weekday == fromWeekday).ToList().Select(name => new PlanDayName { Weekday = target, Name = name.Name }));
                Exercises.AddRange(Exercises.Where(exercise => exercise.Weekday == fromWeekday).ToList().Select(exercise => new PlanExercise
                {
                    PlanExerciseID = ++_nextId,
                    Weekday = target,
                    ExerciseType = exercise.ExerciseType,
                    Name = exercise.Name,
                    Detail = exercise.Detail,
                    Notes = exercise.Notes,
                    Muscles = exercise.Muscles,
                    SortOrder = exercise.SortOrder,
                }));
            }
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// Logged days in memory: the first write on a date copies that weekday's plan, like dbo.DayEnsure.
/// </summary>
internal sealed class FakeDayRepository(FakePlanRepository plan) : IDayRepository
{
    private long _nextId = 1000;

    public DayRange Data { get; } = DayRange.Empty;

    public List<string> Imports { get; } = [];

    public Task<DayRange> ListRangeAsync(long userId, DateOnly from, DateOnly to)
    {
        var days = Data.Days.Where(day => DateOnly.FromDateTime(day.DayDate) >= from && DateOnly.FromDateTime(day.DayDate) <= to).ToList();
        var ids = days.Select(day => day.DayID).ToHashSet();
        var exercises = Data.Exercises.Where(exercise => ids.Contains(exercise.DayID)).ToList();
        var exerciseIds = exercises.Select(exercise => exercise.DayExerciseID).ToHashSet();
        return Task.FromResult(new DayRange(days, Data.Meals.Where(meal => ids.Contains(meal.DayID)).ToList(), exercises,
            Data.Sets.Where(set => exerciseIds.Contains(set.DayExerciseID)).ToList()));
    }

    public Task<List<ExerciseLogRow>> ListExerciseLogAsync(long userId) => Task.FromResult(
        Data.Exercises
            .Where(exercise => exercise.ExerciseType == ExerciseType.Strength)
            .Select(exercise =>
            {
                var sets = Data.Sets.Where(set => set.DayExerciseID == exercise.DayExerciseID).OrderBy(set => set.SetNumber).ToList();
                return new ExerciseLogRow
                {
                    DayDate = Data.Days.Single(day => day.DayID == exercise.DayID).DayDate,
                    NameKey = exercise.NameKey,
                    Name = exercise.Name,
                    LoadKg = exercise.LoadKg,
                    Reps = sets.Count == 0 ? null : string.Join(',', sets.Select(set => set.Reps)),
                };
            })
            .Where(row => row.LoadKg is not null || row.Reps is not null)
            .OrderBy(row => row.DayDate)
            .ToList());

    public Task<bool> SetMealEatenAsync(long userId, DateOnly date, long planMealId, bool eaten)
    {
        var day = Ensure(date);
        var meal = Data.Meals.FirstOrDefault(candidate => candidate.DayID == day.DayID && candidate.PlanMealID == planMealId);
        if (meal is not null)
        {
            meal.Eaten = eaten;
        }

        return Task.FromResult(meal is not null);
    }

    public Task SetAllMealsEatenAsync(long userId, DateOnly date, bool eaten)
    {
        var day = Ensure(date);
        Data.Meals.Where(meal => meal.DayID == day.DayID).ToList().ForEach(meal => meal.Eaten = eaten);
        return Task.CompletedTask;
    }

    public Task SetTrainingStateAsync(long userId, DateOnly date, TrainingState state)
    {
        Ensure(date).TrainingState = state;
        return Task.CompletedTask;
    }

    public Task<bool> LogExerciseAsync(long userId, DateOnly date, long planExerciseId, decimal? loadKg, IReadOnlyList<int?> sets, decimal? cardioMinutes, decimal? cardioKm)
    {
        var day = Ensure(date);
        var exercise = Data.Exercises.FirstOrDefault(candidate => candidate.DayID == day.DayID && candidate.PlanExerciseID == planExerciseId);
        if (exercise is null)
        {
            return Task.FromResult(false);
        }

        exercise.LoadKg = loadKg;
        exercise.CardioMinutes = cardioMinutes;
        exercise.CardioKm = cardioKm;
        Data.Sets.RemoveAll(set => set.DayExerciseID == exercise.DayExerciseID);
        for (var i = 0; i < sets.Count; i++)
        {
            if (sets[i] is { } reps)
            {
                Data.Sets.Add(new DaySetRow { DayExerciseID = exercise.DayExerciseID, SetNumber = i + 1, Reps = reps });
            }
        }

        if (day.TrainingState == TrainingState.Pending && (loadKg is not null || cardioMinutes is not null || cardioKm is not null || sets.Any(set => set is not null)))
        {
            day.TrainingState = TrainingState.Done;
        }

        return Task.FromResult(true);
    }

    public Task DeleteRangeAsync(long userId, DateOnly from, DateOnly to)
    {
        foreach (var day in Data.Days.Where(day => DateOnly.FromDateTime(day.DayDate) >= from && DateOnly.FromDateTime(day.DayDate) <= to).ToList())
        {
            Data.Days.Remove(day);
            var exercises = Data.Exercises.Where(exercise => exercise.DayID == day.DayID).Select(exercise => exercise.DayExerciseID).ToHashSet();
            Data.Meals.RemoveAll(meal => meal.DayID == day.DayID);
            Data.Exercises.RemoveAll(exercise => exercise.DayID == day.DayID);
            Data.Sets.RemoveAll(set => exercises.Contains(set.DayExerciseID));
        }

        return Task.CompletedTask;
    }

    public Task ImportAsync(long userId, string days)
    {
        Imports.Add(days);
        return Task.CompletedTask;
    }

    private DayRow Ensure(DateOnly date)
    {
        var existing = Data.Days.FirstOrDefault(day => DateOnly.FromDateTime(day.DayDate) == date);
        if (existing is not null)
        {
            return existing;
        }

        var row = new DayRow { DayID = ++_nextId, DayDate = date.ToDateTime(TimeOnly.MinValue) };
        Data.Days.Add(row);
        var weekday = WeekMath.Weekday(date);
        foreach (var meal in plan.Meals.Where(meal => meal.Weekday == weekday))
        {
            Data.Meals.Add(new DayMealRow { DayID = row.DayID, DayMealID = ++_nextId, PlanMealID = meal.PlanMealID, Name = meal.Name, SortOrder = meal.SortOrder, MealTime = meal.MealTime });
        }

        foreach (var exercise in plan.Exercises.Where(exercise => exercise.Weekday == weekday))
        {
            Data.Exercises.Add(new DayExerciseRow
            {
                DayID = row.DayID,
                DayExerciseID = ++_nextId,
                PlanExerciseID = exercise.PlanExerciseID,
                ExerciseType = exercise.ExerciseType,
                Name = exercise.Name,
                NameKey = exercise.Name.ToLowerInvariant(),
                Detail = exercise.Detail,
                Notes = exercise.Notes,
                Muscles = exercise.Muscles,
                SortOrder = exercise.SortOrder,
            });
        }

        return row;
    }
}

/// <summary>Weigh-ins in memory.</summary>
internal sealed class FakeWeightRepository : IWeightRepository
{
    public SortedDictionary<DateOnly, decimal> Entries { get; } = [];

    public Task<List<WeightEntry>> ListAsync(long userId) =>
        Task.FromResult(Entries.Select(pair => new WeightEntry { EntryDate = pair.Key.ToDateTime(TimeOnly.MinValue), WeightKg = pair.Value }).ToList());

    public Task SaveAsync(long userId, DateOnly date, decimal weightKg)
    {
        Entries[date] = weightKg;
        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(long userId, DateOnly date) => Task.FromResult(Entries.Remove(date));

    public Task ImportAsync(long userId, IEnumerable<(DateOnly Date, decimal WeightKg)> entries)
    {
        foreach (var (date, weightKg) in entries)
        {
            Entries[date] = weightKg;
        }

        return Task.CompletedTask;
    }
}

/// <summary>Settings in memory.</summary>
internal sealed class FakeSettingRepository : ISettingRepository
{
    public UserSetting? Setting { get; set; }

    public Task<UserSetting?> GetAsync(long userId) => Task.FromResult(Setting);

    public Task<UserSetting> SaveAsync(long userId, decimal? weightGoalKg, int? restSeconds)
    {
        Setting = new UserSetting { UserID = userId, WeightGoalKg = weightGoalKg, RestSeconds = restSeconds, UpdatedAt = DateTime.UtcNow };
        return Task.FromResult(Setting);
    }
}
