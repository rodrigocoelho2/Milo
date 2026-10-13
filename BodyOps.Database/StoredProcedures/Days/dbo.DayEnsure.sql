/*
    DayEnsure
    Returns the logged day of @DayDate, creating it the first time with a copy of that weekday's plan.
    Called inside the day-writing procedures' transactions.
*/
CREATE PROCEDURE [dbo].[DayEnsure]
    @UserID  BIGINT,
    @DayDate DATE,
    @DayID   BIGINT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Weekday TINYINT = CAST(DATEDIFF(DAY, '19000101', @DayDate) % 7 + 1 AS TINYINT);

    SET @DayID = NULL;

    SELECT @DayID = d.[DayID]
    FROM [dbo].[Day] d WITH (UPDLOCK, HOLDLOCK)
    WHERE d.[UserID] = @UserID
      AND d.[DayDate] = @DayDate;

    IF @DayID IS NOT NULL
        RETURN;

    INSERT INTO [dbo].[Day] ([UserID], [DayDate])
    VALUES (@UserID, @DayDate);

    SET @DayID = SCOPE_IDENTITY();

    INSERT INTO [dbo].[DayMeal] ([DayID], [PlanMealID], [Name], [Foods], [SortOrder])
    SELECT @DayID, pm.[PlanMealID], pm.[Name], pm.[Foods], pm.[SortOrder]
    FROM [dbo].[PlanMealSummary] pm
    WHERE pm.[UserID] = @UserID
      AND pm.[Weekday] = @Weekday;

    INSERT INTO [dbo].[DayExercise] ([DayID], [PlanExerciseID], [ExerciseType], [Name], [NameKey], [Detail], [Notes], [Muscles], [SortOrder])
    SELECT @DayID, pe.[PlanExerciseID], pe.[ExerciseType], pe.[Name], LOWER(pe.[Name]), pe.[Detail], pe.[Notes], pe.[Muscles], pe.[SortOrder]
    FROM [dbo].[PlanExercise] pe
    WHERE pe.[UserID] = @UserID
      AND pe.[Weekday] = @Weekday;
END
