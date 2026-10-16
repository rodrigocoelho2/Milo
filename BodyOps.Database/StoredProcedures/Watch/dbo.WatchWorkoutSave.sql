/*
    WatchWorkoutSave
    Adds a Watch workout, or updates it when the Shortcut sends the same one again (same start). The planned cardio it
    filled in is kept unless a new one is given. Returns the workout (same shape as dbo.WatchWorkoutList).
*/
CREATE PROCEDURE [dbo].[WatchWorkoutSave]
    @UserID          BIGINT,
    @DayDate         DATE,
    @StartedAt       DATETIME2 (0),
    @EndedAt         DATETIME2 (0),
    @Kind            NVARCHAR (60),
    @DurationSeconds INT,
    @ActiveKcal      DECIMAL (7, 1) = NULL,
    @AvgBpm          SMALLINT = NULL,
    @MaxBpm          SMALLINT = NULL,
    @DistanceKm      DECIMAL (7, 2) = NULL,
    @HeartRates      NVARCHAR (1000) = N'',
    @PlanExerciseID  BIGINT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;

    BEGIN TRANSACTION;

    UPDATE [dbo].[WatchWorkout] WITH (UPDLOCK, HOLDLOCK)
    SET [DayDate] = @DayDate,
        [EndedAt] = @EndedAt,
        [Kind] = @Kind,
        [DurationSeconds] = @DurationSeconds,
        [ActiveKcal] = @ActiveKcal,
        [AvgBpm] = @AvgBpm,
        [MaxBpm] = @MaxBpm,
        [DistanceKm] = @DistanceKm,
        [HeartRates] = ISNULL(@HeartRates, N''),
        [PlanExerciseID] = ISNULL(@PlanExerciseID, [PlanExerciseID])
    WHERE [UserID] = @UserID AND [StartedAt] = @StartedAt;

    IF @@ROWCOUNT = 0
    BEGIN
        INSERT INTO [dbo].[WatchWorkout] ([UserID], [DayDate], [StartedAt], [EndedAt], [Kind], [DurationSeconds], [ActiveKcal], [AvgBpm], [MaxBpm], [DistanceKm], [HeartRates], [PlanExerciseID])
        VALUES (@UserID, @DayDate, @StartedAt, @EndedAt, @Kind, @DurationSeconds, @ActiveKcal, @AvgBpm, @MaxBpm, @DistanceKm, ISNULL(@HeartRates, N''), @PlanExerciseID);
    END

    COMMIT TRANSACTION;

    SELECT w.[WatchWorkoutID], w.[UserID], w.[DayDate], w.[StartedAt], w.[EndedAt], w.[Kind], w.[DurationSeconds],
           w.[ActiveKcal], w.[AvgBpm], w.[MaxBpm], w.[DistanceKm], w.[HeartRates], w.[PlanExerciseID], w.[CreatedAt]
    FROM [dbo].[WatchWorkout] w
    WHERE w.[UserID] = @UserID AND w.[StartedAt] = @StartedAt;
END
