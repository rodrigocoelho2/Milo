/*
    WatchKeyGet
    The account's Shortcut key (hint only, never the key), when it was made and last used, and the last error.
*/
CREATE PROCEDURE [dbo].[WatchKeyGet]
    @UserID BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT k.[UserID], k.[KeyHint], k.[CreatedAt], k.[LastUsedAt], k.[LastError]
    FROM [dbo].[WatchKey] k
    WHERE k.[UserID] = @UserID;
END
