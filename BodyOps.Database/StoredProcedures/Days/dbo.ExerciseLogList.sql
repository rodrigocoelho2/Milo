/*
    ExerciseLogList
    Every strength exercise the account has logged with a load or sets, oldest first, with its sets as
    "12,11,10". The WebApi works out last time, personal records and progression from these rows.
*/
CREATE PROCEDURE [dbo].[ExerciseLogList]
    @UserID BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;

    SELECT d.[DayDate],
           de.[NameKey],
           de.[Name],
           de.[LoadKg],
           sx.[Reps]
    FROM [dbo].[DayExercise] de
    INNER JOIN [dbo].[Day] d ON d.[DayID] = de.[DayID]
    OUTER APPLY (
        SELECT STRING_AGG(CAST(s.[Reps] AS NVARCHAR (10)), N',') WITHIN GROUP (ORDER BY s.[SetNumber]) AS [Reps]
        FROM [dbo].[DaySet] s
        WHERE s.[DayExerciseID] = de.[DayExerciseID]
    ) sx
    WHERE d.[UserID] = @UserID
      AND de.[ExerciseType] = 1
      AND (de.[LoadKg] IS NOT NULL OR sx.[Reps] IS NOT NULL)
    ORDER BY d.[DayDate], de.[SortOrder];
END
