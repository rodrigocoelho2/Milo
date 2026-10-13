/*
    UserTouchLogin
    Records a sign-in.
*/
CREATE PROCEDURE [dbo].[UserTouchLogin]
    @UserID BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;

    UPDATE [dbo].[User]
    SET [LastLoginAt] = SYSUTCDATETIME()
    WHERE [UserID] = @UserID;
END
