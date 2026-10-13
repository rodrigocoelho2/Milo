/*
    PlanMealsSave
    Replaces the meals of one weekday with @Json, in order:
        [{"id":12,"name":"Breakfast","time":"08:00","foods":[{"name":"Whey","amount":25,"unit":1}]}, …]
    "time" is optional (null = no set time).
    A meal whose id belongs to this account and weekday is updated (so logged days keep their ticks);
    any other id, or none, adds a meal. Meals left out are deleted. Foods are always replaced.
    Then this and later weeks' logged days of that weekday are brought in line with the plan (dbo.DaySyncFromPlan).
    Returns the weekday's meals and foods (same shape as dbo.PlanGet).
*/
CREATE PROCEDURE [dbo].[PlanMealsSave]
    @UserID   BIGINT,
    @Weekday  TINYINT,
    @Json     NVARCHAR (MAX),
    @SyncFrom DATE
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Meals TABLE ([Ord] INT NOT NULL PRIMARY KEY, [PlanMealID] BIGINT NULL, [Name] NVARCHAR (80) NOT NULL, [MealTime] TIME (0) NULL, [Foods] NVARCHAR (MAX) NULL);
    DECLARE @Map TABLE ([Ord] INT NOT NULL PRIMARY KEY, [PlanMealID] BIGINT NOT NULL);

    INSERT INTO @Meals ([Ord], [PlanMealID], [Name], [MealTime], [Foods])
    SELECT CAST(j.[key] AS INT), m.[Id], m.[Name], TRY_CONVERT(TIME (0), m.[MealTime]), m.[Foods]
    FROM OPENJSON(@Json) j
    CROSS APPLY OPENJSON(j.[value]) WITH ([Id] BIGINT '$.id', [Name] NVARCHAR (80) '$.name', [MealTime] NVARCHAR (8) '$.time', [Foods] NVARCHAR (MAX) '$.foods' AS JSON) m;

    BEGIN TRANSACTION;

    -- Ids from another account or weekday are treated as new meals.
    UPDATE m
    SET [PlanMealID] = NULL
    FROM @Meals m
    WHERE m.[PlanMealID] IS NOT NULL
      AND NOT EXISTS (SELECT 1 FROM [dbo].[PlanMeal] pm WITH (UPDLOCK)
                      WHERE pm.[PlanMealID] = m.[PlanMealID] AND pm.[UserID] = @UserID AND pm.[Weekday] = @Weekday);

    DELETE pm
    FROM [dbo].[PlanMeal] pm
    WHERE pm.[UserID] = @UserID
      AND pm.[Weekday] = @Weekday
      AND NOT EXISTS (SELECT 1 FROM @Meals m WHERE m.[PlanMealID] = pm.[PlanMealID]);

    UPDATE pm
    SET [Name] = m.[Name],
        [SortOrder] = m.[Ord],
        [MealTime] = m.[MealTime]
    FROM [dbo].[PlanMeal] pm
    INNER JOIN @Meals m ON m.[PlanMealID] = pm.[PlanMealID];

    INSERT INTO @Map ([Ord], [PlanMealID])
    SELECT m.[Ord], m.[PlanMealID] FROM @Meals m WHERE m.[PlanMealID] IS NOT NULL;

    MERGE [dbo].[PlanMeal] AS target
    USING (SELECT m.[Ord], m.[Name], m.[MealTime] FROM @Meals m WHERE m.[PlanMealID] IS NULL) AS source
    ON 1 = 0
    WHEN NOT MATCHED THEN
        INSERT ([UserID], [Weekday], [Name], [SortOrder], [MealTime]) VALUES (@UserID, @Weekday, source.[Name], source.[Ord], source.[MealTime])
    OUTPUT source.[Ord], inserted.[PlanMealID] INTO @Map ([Ord], [PlanMealID]);

    DELETE f
    FROM [dbo].[PlanFood] f
    INNER JOIN @Map map ON map.[PlanMealID] = f.[PlanMealID];

    INSERT INTO [dbo].[PlanFood] ([PlanMealID], [Name], [Amount], [Unit], [SortOrder])
    SELECT map.[PlanMealID], fd.[Name], fd.[Amount], fd.[Unit], CAST(fj.[key] AS INT)
    FROM @Meals m
    INNER JOIN @Map map ON map.[Ord] = m.[Ord]
    CROSS APPLY OPENJSON(ISNULL(m.[Foods], N'[]')) fj
    CROSS APPLY OPENJSON(fj.[value]) WITH ([Name] NVARCHAR (80) '$.name', [Amount] DECIMAL (8, 2) '$.amount', [Unit] TINYINT '$.unit') fd;

    EXEC [dbo].[DaySyncFromPlan] @UserID = @UserID, @FromDate = @SyncFrom, @Weekday = @Weekday;

    COMMIT TRANSACTION;

    SELECT pm.[PlanMealID],
           pm.[Weekday],
           pm.[Name],
           pm.[SortOrder],
           pm.[MealTime]
    FROM [dbo].[PlanMeal] pm
    WHERE pm.[UserID] = @UserID
      AND pm.[Weekday] = @Weekday
    ORDER BY pm.[SortOrder], pm.[PlanMealID];

    SELECT f.[PlanFoodID],
           f.[PlanMealID],
           f.[Name],
           f.[Amount],
           CAST(f.[Unit] AS INT) AS [Unit],
           f.[SortOrder]
    FROM [dbo].[PlanFood] f
    INNER JOIN [dbo].[PlanMeal] pm ON pm.[PlanMealID] = f.[PlanMealID]
    WHERE pm.[UserID] = @UserID
      AND pm.[Weekday] = @Weekday
    ORDER BY f.[PlanMealID], f.[SortOrder], f.[PlanFoodID];
END
