/*
    WatchKeyUse
    Finds the account a Shortcut key belongs to and records the use. Returns no row for an unknown key.
*/
CREATE PROCEDURE [dbo].[WatchKeyUse]
    @KeyHash BINARY (32)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE [dbo].[WatchKey]
    SET [LastUsedAt] = SYSUTCDATETIME()
    OUTPUT inserted.[UserID], inserted.[KeyHint], inserted.[CreatedAt], inserted.[LastUsedAt], inserted.[LastError]
    WHERE [KeyHash] = @KeyHash;
END
