using System.Globalization;
using System.Text;
using BodyOps.Library.Enums;

namespace BodyOps.WebApi.Features.Plans.Services;

/// <summary>
/// Works out which muscles an exercise trains from its name (English or Portuguese), unless they were picked by hand.
/// Rules run from specific to generic and the first match wins, so "Leg curl" is hamstrings, not biceps.
/// Keywords match whole words only: "lateral" must never match "lat".
/// </summary>
internal static class MuscleDetector
{
    private static readonly Rule[] Rules =
    [
        new(["triceps", "tricep", "french press", "skull crusher", "skull crushers", "kickback", "kickbacks", "pushdown", "push down",
             "dip", "dips", "frances", "testa", "mergulho", "paralelas"],
            new() { [Muscle.Triceps] = 1 }),
        new(["leg curl", "leg curls", "lying curl", "seated curl", "hamstring", "hamstrings", "nordic", "mesa flexora", "cadeira flexora",
             "flexora", "flexao de pernas", "flexoes de pernas", "femoral", "isquio", "isquiotibiais"],
            new() { [Muscle.Hamstrings] = 1 }),
        new(["wrist curl", "wrist curls", "forearm", "forearms", "farmer", "farmers", "antebraco", "antebracos", "punho", "punhos"],
            new() { [Muscle.Forearms] = 1 }),
        new(["biceps", "bicep", "curl", "curls", "hammer", "preacher", "scott", "rosca", "martelo"],
            new() { [Muscle.Biceps] = 1, [Muscle.Forearms] = 0.4 }),
        new(["shrug", "shrugs", "traps", "encolhimento", "encolhimentos", "trapezio"],
            new() { [Muscle.Traps] = 1 }),
        new(["face pull", "face pulls", "rear delt", "rear delts", "reverse fly", "reverse flye", "reverse pec deck", "posterior ombro",
             "crucifixo inverso", "crucifixo invertido", "voador inverso", "pec deck inverso"],
            new() { [Muscle.Shoulders] = 1, [Muscle.Traps] = 0.5, [Muscle.Lats] = 0.4 }),
        new(["lateral raise", "lateral raises", "front raise", "front raises", "shoulder press", "overhead press", "military press", "arnold",
             "shoulder", "shoulders", "delt", "delts", "deltoid", "deltoids", "elevacao lateral", "elevacoes laterais", "elevacao frontal",
             "elevacoes frontais", "desenvolvimento", "militar", "ombro", "ombros", "deltoide", "deltoides"],
            new() { [Muscle.Shoulders] = 1, [Muscle.Triceps] = 0.3 }),
        new(["leg extension", "leg extensions", "cadeira extensora", "extensora", "extensao de pernas", "extensoes de pernas"],
            new() { [Muscle.Quads] = 1 }),
        new(["hip thrust", "hip thrusts", "glute", "glutes", "abductor", "abductors", "abduction", "elevacao pelvica", "gluteo", "gluteos",
             "abdutora", "abducao", "coice", "coices"],
            new() { [Muscle.Glutes] = 1, [Muscle.Hamstrings] = 0.3 }),
        new(["deadlift", "deadlifts", "romanian", "rdl", "stiff", "good morning", "peso morto", "levantamento terra", "romeno"],
            new() { [Muscle.LowerBack] = 1, [Muscle.Hamstrings] = 0.8, [Muscle.Glutes] = 0.7, [Muscle.Lats] = 0.4, [Muscle.Traps] = 0.3 }),
        new(["hyperextension", "hyperextensions", "back extension", "back extensions", "lower back", "hiperextensao", "hiperextensoes",
             "extensao lombar", "lombar"],
            new() { [Muscle.LowerBack] = 1, [Muscle.Glutes] = 0.4 }),
        new(["squat", "squats", "leg press", "hack", "lunge", "lunges", "split squat", "bulgarian", "step up", "step ups",
             "agachamento", "agachamentos", "prensa", "afundo", "afundos", "avanco", "bulgaro"],
            new() { [Muscle.Quads] = 1, [Muscle.Glutes] = 0.7, [Muscle.Hamstrings] = 0.3, [Muscle.LowerBack] = 0.2 }),
        new(["calf", "calves", "gemeos", "panturrilha", "panturrilhas"],
            new() { [Muscle.Calves] = 1 }),
        new(["abs", "crunch", "crunches", "plank", "planks", "leg raise", "leg raises", "sit up", "sit ups", "core", "oblique", "obliques",
             "abdominal", "abdominais", "prancha", "elevacao de pernas", "abdomen", "obliquo", "obliquos"],
            new() { [Muscle.Abs] = 1 }),
        new(["row", "rows", "pulldown", "pulldowns", "pull down", "lat", "lats", "pull up", "pull ups", "pullup", "pullups", "chin up",
             "chin ups", "pullover", "back", "remada", "remadas", "puxada", "puxadas", "puxador", "barra fixa", "dominada", "dominadas",
             "costas", "dorsal", "dorsais", "latissimo"],
            new() { [Muscle.Lats] = 1, [Muscle.Biceps] = 0.5, [Muscle.Traps] = 0.4, [Muscle.Forearms] = 0.3 }),
        new(["bench press", "chest press", "chest", "pec deck", "peck deck", "fly", "flys", "flye", "flyes", "crossover", "cross over",
             "push up", "push ups", "pushup", "pushups", "press", "supino", "peito", "peitoral", "crucifixo", "voador", "flexao", "flexoes"],
            new() { [Muscle.Chest] = 1, [Muscle.Triceps] = 0.5, [Muscle.Shoulders] = 0.4 }),
    ];

    /// <summary>
    /// The muscles of an exercise and how much each one works (1 = main mover).
    /// </summary>
    /// <param name="name">The exercise name.</param>
    /// <param name="type">Strength or cardio; cardio trains nothing unless picked by hand.</param>
    /// <param name="chosen">The muscles picked by hand (comma-separated numbers), or null to detect them.</param>
    /// <returns>The muscles with their weight.</returns>
    public static Dictionary<Muscle, double> Detect(string? name, ExerciseType type, string? chosen)
    {
        if (chosen is not null)
        {
            return Parse(chosen).ToDictionary(static muscle => muscle, static _ => 1.0);
        }

        if (type == ExerciseType.Cardio)
        {
            return [];
        }

        var text = Normalize(name);
        if (text.Trim().Length == 0)
        {
            return [];
        }

        foreach (var rule in Rules)
        {
            if (rule.Keywords.Any(keyword => text.Contains(' ' + keyword + ' ', StringComparison.Ordinal)))
            {
                return new Dictionary<Muscle, double>(rule.Weights);
            }
        }

        return [];
    }

    /// <summary>
    /// Reads a stored list of muscles.
    /// </summary>
    /// <param name="chosen">Comma-separated muscle numbers ("" = none).</param>
    /// <returns>The known muscles, in order, without repeats.</returns>
    public static List<Muscle> Parse(string? chosen)
    {
        var muscles = new List<Muscle>();
        if (string.IsNullOrWhiteSpace(chosen))
        {
            return muscles;
        }

        foreach (var part in chosen.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var value)
                && Enum.IsDefined(typeof(Muscle), value)
                && !muscles.Contains((Muscle)value))
            {
                muscles.Add((Muscle)value);
            }
        }

        return muscles;
    }

    /// <summary>
    /// Writes a list of muscles for storage.
    /// </summary>
    /// <param name="muscles">The muscles, or null for "detect from the name".</param>
    /// <returns>Comma-separated numbers, or null.</returns>
    public static string? Format(IEnumerable<Muscle>? muscles) =>
        muscles is null ? null : string.Join(',', muscles.Where(static muscle => Enum.IsDefined(muscle)).Distinct().Select(static muscle => ((int)muscle).ToString(CultureInfo.InvariantCulture)));

    // Lower case, no accents, punctuation as spaces, padded so " keyword " is a whole-word test.
    internal static string Normalize(string? value)
    {
        var decomposed = (value ?? string.Empty).Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length + 2).Append(' ');
        var lastWasSpace = true;
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
                lastWasSpace = false;
            }
            else if (!lastWasSpace)
            {
                builder.Append(' ');
                lastWasSpace = true;
            }
        }

        if (!lastWasSpace)
        {
            builder.Append(' ');
        }

        return builder.ToString();
    }

    private sealed record Rule(string[] Keywords, Dictionary<Muscle, double> Weights);
}
