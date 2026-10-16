/*
    PlanExercisesSave
    Replaces the exercises of one weekday with @Json, in order:
        [{"id":40,"type":1,"name":"Pec deck","detail":"3x8-12","notes":"","muscles":null,"cardioKind":null,"progressStepKg":2.5}, …]
    "muscles" is a comma-separated list of muscle numbers, or null to detect them from the name.
    "cardioKind" (cardio only) is 1–9 (see dbo.PlanExercise), or null to work it out from the name.
    "progressStepKg" (strength only) is the weight added when a rep range is completed (0.5–20), or null for 2.5 kg.
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
        [Muscles]        NVARCHAR (60)  NULL,
        [CardioKind]     TINYINT        NULL,
        [ProgressStepKg] DECIMAL (5, 2) NULL);

    INSERT INTO @Items ([Ord], [PlanExerciseID], [ExerciseType], [Name], [Detail], [Notes], [Muscles], [CardioKind], [ProgressStepKg])
    SELECT CAST(j.[key] AS INT), e.[Id], e.[Type], e.[Name], ISNULL(e.[Detail], N''), ISNULL(e.[Notes], N''), e.[Muscles],
           CASE WHEN e.[Type] = 2 AND e.[CardioKind] BETWEEN 1 AND 9 THEN e.[CardioKind] END,
           CASE WHEN e.[Type] = 1 AND e.[ProgressStepKg] BETWEEN 0.5 AND 20 THEN e.[ProgressStepKg] END
    FROM OPENJSON(@Json) j
    CROSS APPLY OPENJSON(j.[value]) WITH (
        [Id]      BIGINT         '$.id',
        [Type]    TINYINT        '$.type',
        [Name]    NVARCHAR (100) '$.name',
        [Detail]  NVARCHAR (60)  '$.detail',
        [Notes]   NVARCHAR (200) '$.notes',
        [Muscles] NVARCHAR (60)  '$.muscles',
        [CardioKind] TINYINT     '$.cardioKind',
        [ProgressStepKg] DECIMAL (5, 2) '$.progressStepKg') e;

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
        [CardioKind] = i.[CardioKind],
        [ProgressStepKg] = i.[ProgressStepKg],
        [SortOrder] = i.[Ord]
    FROM [dbo].[PlanExercise] pe
    INNER JOIN @Items i ON i.[PlanExerciseID] = pe.[PlanExerciseID];

    INSERT INTO [dbo].[PlanExercise] ([UserID], [Weekday], [ExerciseType], [Name], [Detail], [Notes], [Muscles], [CardioKind], [ProgressStepKg], [SortOrder])
    SELECT @UserID, @Weekday, i.[ExerciseType], i.[Name], i.[Detail], i.[Notes], i.[Muscles], i.[CardioKind], i.[ProgressStepKg], i.[Ord]
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
           CAST(pe.[CardioKind] AS INT) AS [CardioKind],
           pe.[ProgressStepKg],
           pe.[SortOrder]
    FROM [dbo].[PlanExercise] pe
    WHERE pe.[UserID] = @UserID
      AND pe.[Weekday] = @Weekday
    ORDER BY pe.[SortOrder], pe.[PlanExerciseID];
END
