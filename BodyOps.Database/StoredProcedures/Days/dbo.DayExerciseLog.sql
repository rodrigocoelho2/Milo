/*
    DayExerciseLog
    Saves what was done for one exercise on a day: the load, the reps of each set (@SetsJson, e.g. [12,11,null]:
    null = set not done) or, for cardio, minutes and kilometres. Logging anything on a day that isn't marked yet
    marks it as trained. @Found = 0 when that day has no such exercise.
*/
CREATE PROCEDURE [dbo].[DayExerciseLog]
    @UserID         BIGINT,
    @DayDate        DATE,
    @PlanExerciseID BIGINT,
    @LoadKg         DECIMAL (6, 2) = NULL,
    @CardioMinutes  DECIMAL (6, 1) = NULL,
    @CardioKm       DECIMAL (6, 2) = NULL,
    @SetsJson       NVARCHAR (400) = N'[]',
    @Found          BIT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;

    DECLARE @DayID BIGINT;
    DECLARE @DayExerciseID BIGINT = NULL;

    BEGIN TRANSACTION;

    EXEC [dbo].[DayEnsure] @UserID = @UserID, @DayDate = @DayDate, @DayID = @DayID OUTPUT;

    SELECT @DayExerciseID = de.[DayExerciseID]
    FROM [dbo].[DayExercise] de
    WHERE de.[DayID] = @DayID
      AND de.[PlanExerciseID] = @PlanExerciseID;

    SET @Found = CASE WHEN @DayExerciseID IS NULL THEN 0 ELSE 1 END;

    IF @DayExerciseID IS NOT NULL
    BEGIN
        UPDATE [dbo].[DayExercise]
        SET [LoadKg] = @LoadKg,
            [CardioMinutes] = @CardioMinutes,
            [CardioKm] = @CardioKm
        WHERE [DayExerciseID] = @DayExerciseID;

        DELETE FROM [dbo].[DaySet] WHERE [DayExerciseID] = @DayExerciseID;

        INSERT INTO [dbo].[DaySet] ([DayExerciseID], [SetNumber], [Reps])
        SELECT @DayExerciseID, CAST(j.[key] AS INT) + 1, CAST(j.[value] AS SMALLINT)
        FROM OPENJSON(ISNULL(@SetsJson, N'[]')) j
        WHERE j.[type] = 2;   -- numbers only; null = set not done

        UPDATE [dbo].[Day]
        SET [TrainingState] = CASE
                WHEN [TrainingState] = 0 AND (@LoadKg IS NOT NULL OR @CardioMinutes IS NOT NULL OR @CardioKm IS NOT NULL
                                              OR EXISTS (SELECT 1 FROM [dbo].[DaySet] s WHERE s.[DayExerciseID] = @DayExerciseID))
                THEN 1 ELSE [TrainingState] END,
            [UpdatedAt] = SYSUTCDATETIME()
        WHERE [DayID] = @DayID;
    END

    COMMIT TRANSACTION;
END
