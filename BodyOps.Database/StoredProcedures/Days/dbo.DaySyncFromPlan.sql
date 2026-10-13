/*
    DaySyncFromPlan
    Brings the logged days on or after @FromDate (the start of the current week) in line with the plan: planned meals
    and exercises that were removed disappear, renamed ones change, new ones appear. Ticks, loads and sets of the items
    that stay are kept. Days before @FromDate are history and are never touched. @Weekday limits it to one weekday.
    Called inside the plan-saving procedures' transactions.
*/
CREATE PROCEDURE [dbo].[DaySyncFromPlan]
    @UserID   BIGINT,
    @FromDate DATE,
    @Weekday  TINYINT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Days TABLE ([DayID] BIGINT NOT NULL PRIMARY KEY, [Weekday] TINYINT NOT NULL);

    INSERT INTO @Days ([DayID], [Weekday])
    SELECT x.[DayID], x.[Weekday]
    FROM (
        SELECT d.[DayID], CAST(DATEDIFF(DAY, '19000101', d.[DayDate]) % 7 + 1 AS TINYINT) AS [Weekday]
        FROM [dbo].[Day] d
        WHERE d.[UserID] = @UserID
          AND d.[DayDate] >= @FromDate
    ) x
    WHERE @Weekday IS NULL OR x.[Weekday] = @Weekday;

    IF NOT EXISTS (SELECT 1 FROM @Days)
        RETURN;

    -- Meals
    DELETE dm
    FROM [dbo].[DayMeal] dm
    INNER JOIN @Days d ON d.[DayID] = dm.[DayID]
    WHERE NOT EXISTS (SELECT 1 FROM [dbo].[PlanMeal] pm
                      WHERE pm.[PlanMealID] = dm.[PlanMealID] AND pm.[UserID] = @UserID AND pm.[Weekday] = d.[Weekday]);

    UPDATE dm
    SET [Name] = pm.[Name],
        [Foods] = pm.[Foods],
        [SortOrder] = pm.[SortOrder]
    FROM [dbo].[DayMeal] dm
    INNER JOIN @Days d ON d.[DayID] = dm.[DayID]
    INNER JOIN [dbo].[PlanMealSummary] pm ON pm.[PlanMealID] = dm.[PlanMealID];

    INSERT INTO [dbo].[DayMeal] ([DayID], [PlanMealID], [Name], [Foods], [SortOrder])
    SELECT d.[DayID], pm.[PlanMealID], pm.[Name], pm.[Foods], pm.[SortOrder]
    FROM @Days d
    INNER JOIN [dbo].[PlanMealSummary] pm ON pm.[UserID] = @UserID AND pm.[Weekday] = d.[Weekday]
    WHERE NOT EXISTS (SELECT 1 FROM [dbo].[DayMeal] dm WHERE dm.[DayID] = d.[DayID] AND dm.[PlanMealID] = pm.[PlanMealID]);

    -- Exercises
    DELETE de
    FROM [dbo].[DayExercise] de
    INNER JOIN @Days d ON d.[DayID] = de.[DayID]
    WHERE NOT EXISTS (SELECT 1 FROM [dbo].[PlanExercise] pe
                      WHERE pe.[PlanExerciseID] = de.[PlanExerciseID] AND pe.[UserID] = @UserID AND pe.[Weekday] = d.[Weekday]);

    UPDATE de
    SET [ExerciseType] = pe.[ExerciseType],
        [Name] = pe.[Name],
        [NameKey] = LOWER(pe.[Name]),
        [Detail] = pe.[Detail],
        [Notes] = pe.[Notes],
        [Muscles] = pe.[Muscles],
        [SortOrder] = pe.[SortOrder]
    FROM [dbo].[DayExercise] de
    INNER JOIN @Days d ON d.[DayID] = de.[DayID]
    INNER JOIN [dbo].[PlanExercise] pe ON pe.[PlanExerciseID] = de.[PlanExerciseID];

    INSERT INTO [dbo].[DayExercise] ([DayID], [PlanExerciseID], [ExerciseType], [Name], [NameKey], [Detail], [Notes], [Muscles], [SortOrder])
    SELECT d.[DayID], pe.[PlanExerciseID], pe.[ExerciseType], pe.[Name], LOWER(pe.[Name]), pe.[Detail], pe.[Notes], pe.[Muscles], pe.[SortOrder]
    FROM @Days d
    INNER JOIN [dbo].[PlanExercise] pe ON pe.[UserID] = @UserID AND pe.[Weekday] = d.[Weekday]
    WHERE NOT EXISTS (SELECT 1 FROM [dbo].[DayExercise] de WHERE de.[DayID] = d.[DayID] AND de.[PlanExerciseID] = pe.[PlanExerciseID]);
END
