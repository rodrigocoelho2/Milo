/*
    UserSettingSave
    Creates or replaces the account's settings (every column; NULL = the default) and returns them.
*/
CREATE PROCEDURE [dbo].[UserSettingSave]
    @UserID         BIGINT,
    @WeightGoalKg   DECIMAL (5, 1) = NULL,
    @RestSeconds    SMALLINT       = NULL,
    @WeighEveryDays TINYINT        = NULL,
    @RemindMeals    BIT            = NULL,
    @RemindWorkout  BIT            = NULL,
    @WorkoutTime    TIME (0)       = NULL,
    @RemindWeighIn  BIT            = NULL,
    @WeighInTime    TIME (0)       = NULL,
    @RemindStreak   BIT            = NULL,
    @StreakTime     TIME (0)       = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;

    BEGIN TRANSACTION;

    UPDATE [dbo].[UserSetting] WITH (UPDLOCK, HOLDLOCK)
    SET [WeightGoalKg] = @WeightGoalKg,
        [RestSeconds] = @RestSeconds,
        [WeighEveryDays] = @WeighEveryDays,
        [RemindMeals] = @RemindMeals,
        [RemindWorkout] = @RemindWorkout,
        [WorkoutTime] = @WorkoutTime,
        [RemindWeighIn] = @RemindWeighIn,
        [WeighInTime] = @WeighInTime,
        [RemindStreak] = @RemindStreak,
        [StreakTime] = @StreakTime,
        [UpdatedAt] = SYSUTCDATETIME()
    WHERE [UserID] = @UserID;

    IF @@ROWCOUNT = 0
    BEGIN
        INSERT INTO [dbo].[UserSetting] ([UserID], [WeightGoalKg], [RestSeconds], [WeighEveryDays], [RemindMeals], [RemindWorkout], [WorkoutTime], [RemindWeighIn], [WeighInTime], [RemindStreak], [StreakTime])
        VALUES (@UserID, @WeightGoalKg, @RestSeconds, @WeighEveryDays, @RemindMeals, @RemindWorkout, @WorkoutTime, @RemindWeighIn, @WeighInTime, @RemindStreak, @StreakTime);
    END

    COMMIT TRANSACTION;

    SELECT s.[UserID],
           s.[WeightGoalKg],
           CAST(s.[RestSeconds] AS INT) AS [RestSeconds],
           CAST(s.[WeighEveryDays] AS INT) AS [WeighEveryDays],
           s.[RemindMeals],
           s.[RemindWorkout],
           s.[WorkoutTime],
           s.[RemindWeighIn],
           s.[WeighInTime],
           s.[RemindStreak],
           s.[StreakTime],
           s.[UpdatedAt]
    FROM [dbo].[UserSetting] s
    WHERE s.[UserID] = @UserID;
END
