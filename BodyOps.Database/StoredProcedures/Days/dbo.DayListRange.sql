/*
    DayListRange
    The logged days from @From to @To (inclusive) in four result sets: days, their meals, their exercises and the sets.
    Dates with nothing logged have no row; the WebApi shows the plan for those.
*/
CREATE PROCEDURE [dbo].[DayListRange]
    @UserID BIGINT,
    @From   DATE,
    @To     DATE
AS
BEGIN
    SET NOCOUNT ON;
    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;

    SELECT d.[DayID],
           d.[DayDate],
           CAST(d.[TrainingState] AS INT) AS [TrainingState]
    FROM [dbo].[Day] d
    WHERE d.[UserID] = @UserID
      AND d.[DayDate] BETWEEN @From AND @To
    ORDER BY d.[DayDate];

    SELECT dm.[DayID],
           dm.[DayMealID],
           dm.[PlanMealID],
           dm.[Name],
           dm.[Foods],
           dm.[SortOrder],
           dm.[Eaten],
           dm.[MealTime]
    FROM [dbo].[DayMeal] dm
    INNER JOIN [dbo].[Day] d ON d.[DayID] = dm.[DayID]
    WHERE d.[UserID] = @UserID
      AND d.[DayDate] BETWEEN @From AND @To
    ORDER BY dm.[DayID], dm.[SortOrder], dm.[DayMealID];

    SELECT de.[DayID],
           de.[DayExerciseID],
           de.[PlanExerciseID],
           CAST(de.[ExerciseType] AS INT) AS [ExerciseType],
           de.[Name],
           de.[NameKey],
           de.[Detail],
           de.[Notes],
           de.[Muscles],
           de.[SortOrder],
           de.[LoadKg],
           de.[CardioMinutes],
           de.[CardioKm],
           CAST(de.[CardioKind] AS INT) AS [CardioKind]
    FROM [dbo].[DayExercise] de
    INNER JOIN [dbo].[Day] d ON d.[DayID] = de.[DayID]
    WHERE d.[UserID] = @UserID
      AND d.[DayDate] BETWEEN @From AND @To
    ORDER BY de.[DayID], de.[SortOrder], de.[DayExerciseID];

    SELECT s.[DayExerciseID],
           s.[SetNumber],
           s.[Reps],
           s.[LoadKg]
    FROM [dbo].[DaySet] s
    INNER JOIN [dbo].[DayExercise] de ON de.[DayExerciseID] = s.[DayExerciseID]
    INNER JOIN [dbo].[Day] d ON d.[DayID] = de.[DayID]
    WHERE d.[UserID] = @UserID
      AND d.[DayDate] BETWEEN @From AND @To
    ORDER BY s.[DayExerciseID], s.[SetNumber];
END
