/*
    WatchKeyDelete
    Revokes the account's Shortcut key. The workouts already sent stay.
*/
CREATE PROCEDURE [dbo].[WatchKeyDelete]
    @UserID BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    DELETE FROM [dbo].[WatchKey] WHERE [UserID] = @UserID;
END
