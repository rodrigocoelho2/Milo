using BodyOps.WebApi.Features.Days.Models.DTOs;

namespace BodyOps.WebApi.Features.Plans.Models.DTOs;

/// <summary>
/// The plan of one weekday.
/// </summary>
public sealed class PlanDayDTO
{
    /// <summary>Gets or sets the weekday (1 = Monday … 7 = Sunday).</summary>
    public int Weekday { get; set; }

    /// <summary>Gets or sets the meals, in order.</summary>
    public List<PlanMealDTO> Meals { get; set; } = [];

    /// <summary>Gets or sets the exercises, in order.</summary>
    public List<PlanExerciseDTO> Exercises { get; set; } = [];

    /// <summary>Gets or sets the planned sets per muscle (secondary movers count partly), most first.</summary>
    public List<MuscleSetsDTO> Muscles { get; set; } = [];
}
