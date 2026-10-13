/*
    UserSettingGet
    The account's settings. No row yet means the defaults (no weight goal).
*/
CREATE PROCEDURE [dbo].[UserSettingGet]
    @UserID BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;

    SELECT s.[UserID],
           s.[WeightGoalKg],
           s.[UpdatedAt]
    FROM [dbo].[UserSetting] s
    WHERE s.[UserID] = @UserID;
END
