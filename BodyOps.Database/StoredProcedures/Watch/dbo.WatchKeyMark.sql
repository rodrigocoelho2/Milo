/*
    WatchKeyMark
    Records how the last sync went: empty when it worked, otherwise what was wrong with what the Shortcut sent.
*/
CREATE PROCEDURE [dbo].[WatchKeyMark]
    @UserID    BIGINT,
    @LastError NVARCHAR (200)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE [dbo].[WatchKey] SET [LastError] = ISNULL(@LastError, N'') WHERE [UserID] = @UserID;
END
