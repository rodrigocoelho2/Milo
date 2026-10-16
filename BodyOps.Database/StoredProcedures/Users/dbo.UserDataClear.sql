/*
    UserDataClear
    Removes an account's plan, logged days, weigh-ins, settings, foods and Watch workouts, keeping the account itself
    (and its Shortcut key and push devices).
    Used before importing a backup, which replaces everything.
*/
CREATE PROCEDURE [dbo].[UserDataClear]
    @UserID BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    DELETE FROM [dbo].[Day] WHERE [UserID] = @UserID;
    DELETE FROM [dbo].[PlanMeal] WHERE [UserID] = @UserID;
    DELETE FROM [dbo].[PlanExercise] WHERE [UserID] = @UserID;
    DELETE FROM [dbo].[PlanDay] WHERE [UserID] = @UserID;
    DELETE FROM [dbo].[WeightEntry] WHERE [UserID] = @UserID;
    DELETE FROM [dbo].[UserSetting] WHERE [UserID] = @UserID;
    DELETE FROM [dbo].[Food] WHERE [UserID] = @UserID;
    DELETE FROM [dbo].[WatchWorkout] WHERE [UserID] = @UserID;

    COMMIT TRANSACTION;
END
