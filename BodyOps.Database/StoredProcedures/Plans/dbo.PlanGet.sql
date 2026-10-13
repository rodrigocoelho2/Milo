/*
    PlanGet
    The account's weekly plan in four result sets: meals, their foods, exercises and the day names.
    @Weekday limits it to one day.
*/
CREATE PROCEDURE [dbo].[PlanGet]
    @UserID  BIGINT,
    @Weekday TINYINT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;

    SELECT pm.[PlanMealID],
           pm.[Weekday],
           pm.[Name],
           pm.[SortOrder],
           pm.[MealTime]
    FROM [dbo].[PlanMeal] pm
    WHERE pm.[UserID] = @UserID
      AND (@Weekday IS NULL OR pm.[Weekday] = @Weekday)
    ORDER BY pm.[Weekday], pm.[SortOrder], pm.[PlanMealID];

    SELECT f.[PlanFoodID],
           f.[PlanMealID],
           f.[Name],
           f.[Amount],
           CAST(f.[Unit] AS INT) AS [Unit],
           f.[SortOrder]
    FROM [dbo].[PlanFood] f
    INNER JOIN [dbo].[PlanMeal] pm ON pm.[PlanMealID] = f.[PlanMealID]
    WHERE pm.[UserID] = @UserID
      AND (@Weekday IS NULL OR pm.[Weekday] = @Weekday)
    ORDER BY f.[PlanMealID], f.[SortOrder], f.[PlanFoodID];

    SELECT pe.[PlanExerciseID],
           pe.[Weekday],
           CAST(pe.[ExerciseType] AS INT) AS [ExerciseType],
           pe.[Name],
           pe.[Detail],
           pe.[Notes],
           pe.[Muscles],
           pe.[SortOrder]
    FROM [dbo].[PlanExercise] pe
    WHERE pe.[UserID] = @UserID
      AND (@Weekday IS NULL OR pe.[Weekday] = @Weekday)
    ORDER BY pe.[Weekday], pe.[SortOrder], pe.[PlanExerciseID];

    SELECT pd.[Weekday],
           pd.[Name]
    FROM [dbo].[PlanDay] pd
    WHERE pd.[UserID] = @UserID
      AND (@Weekday IS NULL OR pd.[Weekday] = @Weekday)
    ORDER BY pd.[Weekday];
END
