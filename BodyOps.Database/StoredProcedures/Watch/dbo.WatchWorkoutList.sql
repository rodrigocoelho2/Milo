/*
    WatchWorkoutList
    The account's Watch workouts between two days (both included), oldest first.
*/
CREATE PROCEDURE [dbo].[WatchWorkoutList]
    @UserID BIGINT,
    @From   DATE,
    @To     DATE
AS
BEGIN
    SET NOCOUNT ON;

    SELECT w.[WatchWorkoutID], w.[UserID], w.[DayDate], w.[StartedAt], w.[EndedAt], w.[Kind], w.[DurationSeconds],
           w.[ActiveKcal], w.[AvgBpm], w.[MaxBpm], w.[DistanceKm], w.[HeartRates], w.[PlanExerciseID], w.[CreatedAt]
    FROM [dbo].[WatchWorkout] w
    WHERE w.[UserID] = @UserID AND w.[DayDate] BETWEEN @From AND @To
    ORDER BY w.[StartedAt];
END
