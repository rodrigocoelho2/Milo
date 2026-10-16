/*
    UserDataClear
    Removes an account's plan, logged days, weigh-ins, settings and foods, keeping the account itself.
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

    COMMIT TRANSACTION;
END
