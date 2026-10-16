using System.Text.Json;
using BodyOps.Library.Enums;

namespace BodyOps.WebApi.Features.Plans.Services;

/// <summary>
/// The JSON documents dbo.PlanMealsSave and dbo.PlanExercisesSave read (property names are the procedures' JSON paths).
/// Names must already be clean (<see cref="NameText.Clean"/>).
/// </summary>
internal static class PlanJson
{
    /// <summary>
    /// A meal for dbo.PlanMealsSave.
    /// </summary>
    /// <param name="id">The meal id, or null for a new one.</param>
    /// <param name="name">The name.</param>
    /// <param name="time">When it is usually eaten, or null.</param>
    /// <param name="foods">The foods in order.</param>
    /// <returns>The JSON object.</returns>
    public static object Meal(long? id, string name, TimeSpan? time, IEnumerable<(string Name, decimal Amount, FoodUnit Unit)> foods) => new
    {
        id,
        name,
        time = MealTimeText.Format(time),
        foods = foods.Select(static food => new { name = food.Name, amount = food.Amount, unit = (int)food.Unit }).ToList(),
    };

    /// <summary>
    /// An exercise for dbo.PlanExercisesSave.
    /// </summary>
    /// <param name="id">The exercise id, or null for a new one.</param>
    /// <param name="type">Strength or cardio.</param>
    /// <param name="name">The name.</param>
    /// <param name="detail">The plan text.</param>
    /// <param name="notes">The notes.</param>
    /// <param name="muscles">The muscles picked by hand (stored form), or null.</param>
    /// <param name="cardioKind">The kind of cardio picked by hand, or null (always null for strength).</param>
    /// <returns>The JSON object.</returns>
    public static object Exercise(long? id, ExerciseType type, string name, string detail, string notes, string? muscles, CardioKind? cardioKind = null) => new
    {
        id,
        type = (int)type,
        name,
        detail,
        notes,
        muscles,
        cardioKind = type == ExerciseType.Cardio && cardioKind is { } kind && Enum.IsDefined(kind) ? (int?)kind : null,
    };

    /// <summary>
    /// Serializes a list of meals or exercises.
    /// </summary>
    /// <param name="items">The items.</param>
    /// <returns>The JSON array.</returns>
    public static string Serialize(IEnumerable<object> items) => JsonSerializer.Serialize(items.ToList());
}
