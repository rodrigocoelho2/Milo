/*
    PlanMealSummary
    Each planned meal with its foods written out on one line ("Whey 25 g · Banana 1 pc"), as copied into dbo.DayMeal.
*/
CREATE VIEW [dbo].[PlanMealSummary]
AS
SELECT pm.[PlanMealID],
       pm.[UserID],
       pm.[Weekday],
       pm.[Name],
       pm.[SortOrder],
       ISNULL(LEFT(fx.[Foods], 2000), N'') AS [Foods]
FROM [dbo].[PlanMeal] pm
OUTER APPLY (
    SELECT STRING_AGG(CONVERT(NVARCHAR (MAX), CONCAT(f.[Name], N' ', CAST(CAST(f.[Amount] AS FLOAT) AS NVARCHAR (30)),
                CASE f.[Unit] WHEN 2 THEN N' pc' ELSE N' g' END)), N' · ') WITHIN GROUP (ORDER BY f.[SortOrder]) AS [Foods]
    FROM [dbo].[PlanFood] f
    WHERE f.[PlanMealID] = pm.[PlanMealID]
) fx;
