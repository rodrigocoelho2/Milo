/*
    UserSettingGet
    The account's settings. No row yet (or NULL columns) means the defaults (see dbo.UserSetting).
*/
CREATE PROCEDURE [dbo].[UserSettingGet]
    @UserID BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;

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
