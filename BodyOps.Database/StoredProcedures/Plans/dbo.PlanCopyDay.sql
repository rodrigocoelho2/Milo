/*
    PlanCopyDay
    Copies the meals (@Part = 1, with their foods) or the exercises (@Part = 2) of @FromWeekday onto each weekday in
    @ToWeekdays (a JSON array such as [2,4,5]), replacing what those days had. Logged days from @SyncFrom on follow.
*/
CREATE PROCEDURE [dbo].[PlanCopyDay]
    @UserID      BIGINT,
    @FromWeekday TINYINT,
    @ToWeekdays  NVARCHAR (100),
    @Part        TINYINT,
    @SyncFrom    DATE
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Targets TABLE ([Weekday] TINYINT NOT NULL PRIMARY KEY);
    DECLARE @Map TABLE ([OldID] BIGINT NOT NULL, [NewID] BIGINT NOT NULL);

    INSERT INTO @Targets ([Weekday])
    SELECT DISTINCT CAST(j.[value] AS TINYINT)
    FROM OPENJSON(@ToWeekdays) j
    WHERE CAST(j.[value] AS INT) BETWEEN 1 AND 7
      AND CAST(j.[value] AS INT) <> @FromWeekday;

    BEGIN TRANSACTION;

    IF @Part = 1
    BEGIN
        DELETE pm
        FROM [dbo].[PlanMeal] pm
        INNER JOIN @Targets t ON t.[Weekday] = pm.[Weekday]
        WHERE pm.[UserID] = @UserID;

        MERGE [dbo].[PlanMeal] AS target
        USING (
            SELECT pm.[PlanMealID], pm.[Name], pm.[SortOrder], t.[Weekday]
            FROM [dbo].[PlanMeal] pm
            CROSS JOIN @Targets t
            WHERE pm.[UserID] = @UserID AND pm.[Weekday] = @FromWeekday
        ) AS source
        ON 1 = 0
        WHEN NOT MATCHED THEN
            INSERT ([UserID], [Weekday], [Name], [SortOrder]) VALUES (@UserID, source.[Weekday], source.[Name], source.[SortOrder])
        OUTPUT source.[PlanMealID], inserted.[PlanMealID] INTO @Map ([OldID], [NewID]);

        INSERT INTO [dbo].[PlanFood] ([PlanMealID], [Name], [Amount], [Unit], [SortOrder])
        SELECT map.[NewID], f.[Name], f.[Amount], f.[Unit], f.[SortOrder]
        FROM @Map map
        INNER JOIN [dbo].[PlanFood] f ON f.[PlanMealID] = map.[OldID];
    END
    ELSE IF @Part = 2
    BEGIN
        DELETE pe
        FROM [dbo].[PlanExercise] pe
        INNER JOIN @Targets t ON t.[Weekday] = pe.[Weekday]
        WHERE pe.[UserID] = @UserID;

        INSERT INTO [dbo].[PlanExercise] ([UserID], [Weekday], [ExerciseType], [Name], [Detail], [Notes], [Muscles], [SortOrder])
        SELECT @UserID, t.[Weekday], pe.[ExerciseType], pe.[Name], pe.[Detail], pe.[Notes], pe.[Muscles], pe.[SortOrder]
        FROM [dbo].[PlanExercise] pe
        CROSS JOIN @Targets t
        WHERE pe.[UserID] = @UserID AND pe.[Weekday] = @FromWeekday;
    END

    EXEC [dbo].[DaySyncFromPlan] @UserID = @UserID, @FromDate = @SyncFrom, @Weekday = NULL;

    COMMIT TRANSACTION;
END
