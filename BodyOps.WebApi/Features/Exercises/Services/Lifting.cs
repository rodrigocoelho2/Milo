namespace BodyOps.WebApi.Features.Exercises.Services;

/// <summary>
/// The strength maths: estimated one-rep max, records per rep count and when a rep range earns more weight.
/// Pure functions, unit-tested.
/// </summary>
internal static class Lifting
{
    /// <summary>The most reps a set can have and still count for the estimated 1RM (the formula drifts beyond).</summary>
    public const int MaxRepsForE1rm = 12;

    /// <summary>The weight added when a rep range is completed and the exercise has no step of its own.</summary>
    public const decimal DefaultStepKg = 2.5m;

    /// <summary>The smallest step a plan can set.</summary>
    public const decimal MinStepKg = 0.5m;

    /// <summary>The largest step a plan can set.</summary>
    public const decimal MaxStepKg = 20m;

    /// <summary>The rep counts that keep a record of their own (heaviest load lifted for at least that many reps).</summary>
    public static readonly int[] RepCounts = [3, 5, 8, 10, 12, 15];

    /// <summary>
    /// The estimated one-rep max of a set (Epley: load × (1 + reps / 30); one rep is the load itself).
    /// </summary>
    /// <param name="loadKg">The load.</param>
    /// <param name="reps">The reps.</param>
    /// <returns>The estimate rounded to 0.1 kg, or null without a load or for 0 or more than 12 reps.</returns>
    public static decimal? E1rm(decimal? loadKg, int reps)
    {
        if (loadKg is not { } load || load <= 0 || reps < 1 || reps > MaxRepsForE1rm)
        {
            return null;
        }

        return reps == 1 ? load : Math.Round(load * (1 + (reps / 30m)), 1, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// The best estimated 1RM among a session's sets.
    /// </summary>
    /// <param name="reps">The reps of each set (null = not done).</param>
    /// <param name="loads">The load of each set, same order.</param>
    /// <returns>The best estimate, or null when no set qualifies.</returns>
    public static decimal? BestE1rm(IReadOnlyList<int?> reps, IReadOnlyList<decimal?> loads)
    {
        decimal? best = null;
        for (var i = 0; i < reps.Count; i++)
        {
            if (reps[i] is { } count && E1rm(i < loads.Count ? loads[i] : null, count) is { } estimate && (best is null || estimate > best))
            {
                best = estimate;
            }
        }

        return best;
    }

    /// <summary>
    /// The heaviest load done for at least each of <see cref="RepCounts"/> reps.
    /// </summary>
    /// <param name="sets">Sets as (reps, load).</param>
    /// <returns>Rep count → heaviest load; counts never reached are left out.</returns>
    public static Dictionary<int, decimal> RepBests(IEnumerable<(int Reps, decimal? LoadKg)> sets)
    {
        var bests = new Dictionary<int, decimal>();
        foreach (var (reps, loadKg) in sets)
        {
            if (loadKg is not { } load || load <= 0)
            {
                continue;
            }

            foreach (var count in RepCounts)
            {
                if (reps >= count && (!bests.TryGetValue(count, out var best) || load > best))
                {
                    bests[count] = load;
                }
            }
        }

        return bests;
    }

    /// <summary>
    /// The highest rep count whose record a session beat (a count done for the first time is not a record).
    /// </summary>
    /// <param name="before">The records before the session.</param>
    /// <param name="session">The session's own bests.</param>
    /// <returns>The rep count with the load before and after, or null.</returns>
    public static (int Reps, decimal PreviousKg, decimal LoadKg)? RepRecord(IReadOnlyDictionary<int, decimal> before, IReadOnlyDictionary<int, decimal> session)
    {
        foreach (var count in RepCounts.Reverse())
        {
            if (session.TryGetValue(count, out var load) && before.TryGetValue(count, out var previous) && load > previous)
            {
                return (count, previous, load);
            }
        }

        return null;
    }

    /// <summary>
    /// Whether a session completed its rep range: the plan has a range and every planned set reached its top.
    /// </summary>
    /// <param name="sets">The planned sets (min and max reps).</param>
    /// <param name="reps">The reps done per set (null = not done).</param>
    /// <returns>True when the weight should go up next time.</returns>
    public static bool Earned(IReadOnlyList<(int Min, int Max)> sets, IReadOnlyList<int?> reps)
    {
        if (sets.Count == 0 || !sets.Any(static set => set.Min < set.Max) || reps.Count < sets.Count)
        {
            return false;
        }

        for (var i = 0; i < sets.Count; i++)
        {
            if (reps[i] is not { } done || done < sets[i].Max)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// The step of a plan with a rep range: its own, or <see cref="DefaultStepKg"/>.
    /// </summary>
    /// <param name="isRange">Whether the plan has a range.</param>
    /// <param name="stepKg">The step set on the exercise, or null.</param>
    /// <returns>The step, or null without a range.</returns>
    public static decimal? Step(bool isRange, decimal? stepKg) => isRange ? stepKg ?? DefaultStepKg : null;
}
