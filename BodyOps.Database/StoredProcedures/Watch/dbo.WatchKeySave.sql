/*
    WatchKeySave
    Sets the account's Shortcut key (its SHA-256 and a hint), replacing the old one.
*/
CREATE PROCEDURE [dbo].[WatchKeySave]
    @UserID  BIGINT,
    @KeyHash BINARY (32),
    @KeyHint NVARCHAR (24)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    DELETE FROM [dbo].[WatchKey] WITH (UPDLOCK, HOLDLOCK) WHERE [UserID] = @UserID;

    INSERT INTO [dbo].[WatchKey] ([UserID], [KeyHash], [KeyHint])
    VALUES (@UserID, @KeyHash, @KeyHint);

    COMMIT TRANSACTION;

    SELECT k.[UserID], k.[KeyHint], k.[CreatedAt], k.[LastUsedAt], k.[LastError]
    FROM [dbo].[WatchKey] k
    WHERE k.[UserID] = @UserID;
END
