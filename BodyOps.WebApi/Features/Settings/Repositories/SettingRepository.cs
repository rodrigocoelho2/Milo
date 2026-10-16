using BodyOps.WebApi.Extensions.Data;
using BodyOps.WebApi.Features.Settings.Models.Entities;

namespace BodyOps.WebApi.Features.Settings.Repositories;

/// <summary>
/// Account settings through the dbo.UserSetting* stored procedures.
/// </summary>
/// <param name="connectionFactory">Opens database connections.</param>
internal sealed class SettingRepository(ISqlConnectionFactory connectionFactory) : RepositoryBase(connectionFactory), ISettingRepository
{
    /// <inheritdoc />
    public Task<UserSetting?> GetAsync(long userId) =>
        QuerySingleOrDefaultAsync<UserSetting>("[dbo].[UserSettingGet]", new { UserID = userId });

    /// <inheritdoc />
    public async Task<UserSetting> SaveAsync(long userId, UserSetting setting) =>
        (await QuerySingleOrDefaultAsync<UserSetting>("[dbo].[UserSettingSave]", new
        {
            UserID = userId,
            setting.WeightGoalKg,
            RestSeconds = (short?)setting.RestSeconds,
            WeighEveryDays = (byte?)setting.WeighEveryDays,
            setting.RemindMeals,
            setting.RemindWorkout,
            setting.WorkoutTime,
            setting.RemindWeighIn,
            setting.WeighInTime,
            setting.RemindStreak,
            setting.StreakTime,
        }))!;
}
