/*
    DayImport
    Writes logged days from a backup (or puts back a week after Reset week), replacing those dates. @Json:
    [{"date":"2026-07-21","trainingState":1,
      "meals":[{"planMealId":12,"name":"Breakfast","foods":"Whey 25 g","eaten":true,"time":"08:00"}],
      "exercises":[{"planExerciseId":40,"type":1,"name":"Pec deck","detail":"3x12","notes":"","muscles":null,
                    "loadKg":70,"cardioMinutes":null,"cardioKm":null,"sets":[10,10,10]}]}, …]
*/
CREATE PROCEDURE [dbo].[DayImport]
    @UserID BIGINT,
    @Json   NVARCHAR (MAX)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Days TABLE ([DayDate] DATE NOT NULL PRIMARY KEY, [TrainingState] TINYINT NOT NULL, [Meals] NVARCHAR (MAX) NULL, [Exercises] NVARCHAR (MAX) NULL);
    DECLARE @DayMap TABLE ([DayDate] DATE NOT NULL PRIMARY KEY, [DayID] BIGINT NOT NULL);
    DECLARE @Exercises TABLE (
        [DayDate]        DATE           NOT NULL,
        [Ord]            INT            NOT NULL,
        [PlanExerciseID] BIGINT         NOT NULL,
        [ExerciseType]   TINYINT        NOT NULL,
        [Name]           NVARCHAR (100) NOT NULL,
        [Detail]         NVARCHAR (60)  NOT NULL,
        [Notes]          NVARCHAR (200) NOT NULL,
        [Muscles]        NVARCHAR (60)  NULL,
        [LoadKg]         DECIMAL (6, 2) NULL,
        [CardioMinutes]  DECIMAL (6, 1) NULL,
        [CardioKm]       DECIMAL (6, 2) NULL,
        [Sets]           NVARCHAR (MAX) NULL,
        PRIMARY KEY ([DayDate], [Ord]));
    DECLARE @ExerciseMap TABLE ([DayDate] DATE NOT NULL, [Ord] INT NOT NULL, [DayExerciseID] BIGINT NOT NULL);

    INSERT INTO @Days ([DayDate], [TrainingState], [Meals], [Exercises])
    SELECT d.[DayDate], ISNULL(d.[TrainingState], 0), d.[Meals], d.[Exercises]
    FROM OPENJSON(@Json) WITH (
        [DayDate]       DATE           '$.date',
        [TrainingState] TINYINT        '$.trainingState',
        [Meals]         NVARCHAR (MAX) '$.meals' AS JSON,
        [Exercises]     NVARCHAR (MAX) '$.exercises' AS JSON) d;

    INSERT INTO @Exercises ([DayDate], [Ord], [PlanExerciseID], [ExerciseType], [Name], [Detail], [Notes], [Muscles], [LoadKg], [CardioMinutes], [CardioKm], [Sets])
    SELECT d.[DayDate], CAST(j.[key] AS INT), e.[PlanExerciseID], e.[ExerciseType], e.[Name], ISNULL(e.[Detail], N''), ISNULL(e.[Notes], N''),
           e.[Muscles], e.[LoadKg], e.[CardioMinutes], e.[CardioKm], e.[Sets]
    FROM @Days d
    CROSS APPLY OPENJSON(ISNULL(d.[Exercises], N'[]')) j
    CROSS APPLY OPENJSON(j.[value]) WITH (
        [PlanExerciseID] BIGINT         '$.planExerciseId',
        [ExerciseType]   TINYINT        '$.type',
        [Name]           NVARCHAR (100) '$.name',
        [Detail]         NVARCHAR (60)  '$.detail',
        [Notes]          NVARCHAR (200) '$.notes',
        [Muscles]        NVARCHAR (60)  '$.muscles',
        [LoadKg]         DECIMAL (6, 2) '$.loadKg',
        [CardioMinutes]  DECIMAL (6, 1) '$.cardioMinutes',
        [CardioKm]       DECIMAL (6, 2) '$.cardioKm',
        [Sets]           NVARCHAR (MAX) '$.sets' AS JSON) e;

    BEGIN TRANSACTION;

    DELETE dy
    FROM [dbo].[Day] dy
    INNER JOIN @Days d ON d.[DayDate] = dy.[DayDate]
    WHERE dy.[UserID] = @UserID;

    MERGE [dbo].[Day] AS target
    USING @Days AS source
    ON 1 = 0
    WHEN NOT MATCHED THEN
        INSERT ([UserID], [DayDate], [TrainingState]) VALUES (@UserID, source.[DayDate], source.[TrainingState])
    OUTPUT source.[DayDate], inserted.[DayID] INTO @DayMap ([DayDate], [DayID]);

    INSERT INTO [dbo].[DayMeal] ([DayID], [PlanMealID], [Name], [Foods], [SortOrder], [Eaten], [MealTime])
    SELECT map.[DayID], m.[PlanMealID], m.[Name], ISNULL(m.[Foods], N''), CAST(j.[key] AS INT), ISNULL(m.[Eaten], 0), TRY_CONVERT(TIME (0), m.[MealTime])
    FROM @Days d
    INNER JOIN @DayMap map ON map.[DayDate] = d.[DayDate]
    CROSS APPLY OPENJSON(ISNULL(d.[Meals], N'[]')) j
    CROSS APPLY OPENJSON(j.[value]) WITH (
        [PlanMealID] BIGINT          '$.planMealId',
        [Name]       NVARCHAR (80)   '$.name',
        [Foods]      NVARCHAR (2000) '$.foods',
        [Eaten]      BIT             '$.eaten',
        [MealTime]   NVARCHAR (8)    '$.time') m;

    MERGE [dbo].[DayExercise] AS target
    USING (
        SELECT e.*, map.[DayID]
        FROM @Exercises e
        INNER JOIN @DayMap map ON map.[DayDate] = e.[DayDate]
    ) AS source
    ON 1 = 0
    WHEN NOT MATCHED THEN
        INSERT ([DayID], [PlanExerciseID], [ExerciseType], [Name], [NameKey], [Detail], [Notes], [Muscles], [SortOrder], [LoadKg], [CardioMinutes], [CardioKm])
        VALUES (source.[DayID], source.[PlanExerciseID], source.[ExerciseType], source.[Name], LOWER(source.[Name]), source.[Detail], source.[Notes],
                source.[Muscles], source.[Ord], source.[LoadKg], source.[CardioMinutes], source.[CardioKm])
    OUTPUT source.[DayDate], source.[Ord], inserted.[DayExerciseID] INTO @ExerciseMap ([DayDate], [Ord], [DayExerciseID]);

    INSERT INTO [dbo].[DaySet] ([DayExerciseID], [SetNumber], [Reps])
    SELECT map.[DayExerciseID], CAST(j.[key] AS INT) + 1, CAST(j.[value] AS SMALLINT)
    FROM @Exercises e
    INNER JOIN @ExerciseMap map ON map.[DayDate] = e.[DayDate] AND map.[Ord] = e.[Ord]
    CROSS APPLY OPENJSON(ISNULL(e.[Sets], N'[]')) j
    WHERE j.[type] = 2;

    COMMIT TRANSACTION;
END
