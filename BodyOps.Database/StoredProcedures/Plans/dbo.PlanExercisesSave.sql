/*
    PlanExercisesSave
    Replaces the exercises of one weekday with @Json, in order:
        [{"id":40,"type":1,"name":"Pec deck","detail":"3x12","notes":"","muscles":null}, …]
    "muscles" is a comma-separated list of muscle numbers, or null to detect them from the name.
    An exercise whose id belongs to this account and weekday is updated (logged days keep their loads and sets);
    any other id, or none, adds one. Exercises left out are deleted.
    Then this and later weeks' logged days of that weekday follow the plan (dbo.DaySyncFromPlan).
    Returns the weekday's exercises (same shape as the third result set of dbo.PlanGet).
*/
CREATE PROCEDURE [dbo].[PlanExercisesSave]
    @UserID   BIGINT,
    @Weekday  TINYINT,
    @Json     NVARCHAR (MAX),
    @SyncFrom DATE
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Items TABLE (
        [Ord]            INT            NOT NULL PRIMARY KEY,
        [PlanExerciseID] BIGINT         NULL,
        [ExerciseType]   TINYINT        NOT NULL,
        [Name]           NVARCHAR (100) NOT NULL,
        [Detail]         NVARCHAR (60)  NOT NULL,
        [Notes]          NVARCHAR (200) NOT NULL,
        [Muscles]        NVARCHAR (60)  NULL);

    INSERT INTO @Items ([Ord], [PlanExerciseID], [ExerciseType], [Name], [Detail], [Notes], [Muscles])
    SELECT CAST(j.[key] AS INT), e.[Id], e.[Type], e.[Name], ISNULL(e.[Detail], N''), ISNULL(e.[Notes], N''), e.[Muscles]
    FROM OPENJSON(@Json) j
    CROSS APPLY OPENJSON(j.[value]) WITH (
        [Id]      BIGINT         '$.id',
        [Type]    TINYINT        '$.type',
        [Name]    NVARCHAR (100) '$.name',
        [Detail]  NVARCHAR (60)  '$.detail',
        [Notes]   NVARCHAR (200) '$.notes',
        [Muscles] NVARCHAR (60)  '$.muscles') e;

    BEGIN TRANSACTION;

    UPDATE i
    SET [PlanExerciseID] = NULL
    FROM @Items i
    WHERE i.[PlanExerciseID] IS NOT NULL
      AND NOT EXISTS (SELECT 1 FROM [dbo].[PlanExercise] pe WITH (UPDLOCK)
                      WHERE pe.[PlanExerciseID] = i.[PlanExerciseID] AND pe.[UserID] = @UserID AND pe.[Weekday] = @Weekday);

    DELETE pe
    FROM [dbo].[PlanExercise] pe
    WHERE pe.[UserID] = @UserID
      AND pe.[Weekday] = @Weekday
      AND NOT EXISTS (SELECT 1 FROM @Items i WHERE i.[PlanExerciseID] = pe.[PlanExerciseID]);

    UPDATE pe
    SET [ExerciseType] = i.[ExerciseType],
        [Name] = i.[Name],
        [Detail] = i.[Detail],
        [Notes] = i.[Notes],
        [Muscles] = i.[Muscles],
        [SortOrder] = i.[Ord]
    FROM [dbo].[PlanExercise] pe
    INNER JOIN @Items i ON i.[PlanExerciseID] = pe.[PlanExerciseID];

    INSERT INTO [dbo].[PlanExercise] ([UserID], [Weekday], [ExerciseType], [Name], [Detail], [Notes], [Muscles], [SortOrder])
    SELECT @UserID, @Weekday, i.[ExerciseType], i.[Name], i.[Detail], i.[Notes], i.[Muscles], i.[Ord]
    FROM @Items i
    WHERE i.[PlanExerciseID] IS NULL;

    EXEC [dbo].[DaySyncFromPlan] @UserID = @UserID, @FromDate = @SyncFrom, @Weekday = @Weekday;

    COMMIT TRANSACTION;

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
      AND pe.[Weekday] = @Weekday
    ORDER BY pe.[SortOrder], pe.[PlanExerciseID];
END
