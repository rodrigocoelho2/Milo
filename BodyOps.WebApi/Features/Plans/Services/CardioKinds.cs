using BodyOps.Library.Enums;

namespace BodyOps.WebApi.Features.Plans.Services;

/// <summary>
/// The kind of a cardio exercise: picked in the plan, or worked out from its name (English or Portuguese), and
/// whether it is logged with a distance as well as the time.
/// </summary>
internal static class CardioKinds
{
    private static readonly (CardioKind Kind, string[] Words)[] Rules =
    [
        (CardioKind.Treadmill, ["treadmill", "passadeira", "tapete", "esteira"]),
        (CardioKind.Elliptical, ["elliptical", "cross trainer", "crosstrainer", "eliptica", "eliptico"]),
        (CardioKind.Stairs, ["stair", "stairs", "stairmaster", "stepper", "escada", "escadas", "step"]),
        (CardioKind.Rowing, ["row", "rowing", "rower", "remo", "remadora"]),
        (CardioKind.Swim, ["swim", "swimming", "natacao", "nadar", "piscina"]),
        (CardioKind.Bike, ["bike", "bicycle", "cycling", "cycle", "spinning", "spin", "bicicleta", "ciclismo", "bina"]),
        (CardioKind.Walk, ["walk", "walking", "hike", "caminhada", "caminhar", "andar"]),
        (CardioKind.Run, ["run", "running", "jog", "jogging", "corrida", "correr"]),
    ];

    /// <summary>
    /// The kind to use: the one picked, otherwise the one the name suggests, otherwise <see cref="CardioKind.Other"/>.
    /// </summary>
    /// <param name="name">The exercise name.</param>
    /// <param name="picked">The kind picked in the plan, or null.</param>
    /// <returns>The kind.</returns>
    public static CardioKind Resolve(string? name, CardioKind? picked) =>
        picked is { } kind && Enum.IsDefined(kind) ? kind : Detect(name);

    /// <summary>
    /// The kind a name suggests ("Passadeira" → treadmill, "Bicicleta" → bike).
    /// </summary>
    /// <param name="name">The exercise name.</param>
    /// <returns>The kind, or <see cref="CardioKind.Other"/>.</returns>
    public static CardioKind Detect(string? name)
    {
        var text = MuscleDetector.Normalize(name);
        foreach (var (kind, words) in Rules)
        {
            if (words.Any(word => text.Contains($" {word} ", StringComparison.Ordinal)))
            {
                return kind;
            }
        }

        return CardioKind.Other;
    }

    /// <summary>
    /// Whether the kind is logged with a distance (treadmill, elliptical and stairs are time only).
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns>True for time and distance.</returns>
    public static bool UsesDistance(CardioKind kind) => kind is not (CardioKind.Treadmill or CardioKind.Elliptical or CardioKind.Stairs);
}
