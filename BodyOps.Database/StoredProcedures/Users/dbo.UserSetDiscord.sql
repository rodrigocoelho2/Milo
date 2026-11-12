/*
    UserSetDiscord
    Links a Discord account to an account (NULL @DiscordID unlinks it). Returns Done = 0 when that Discord account is
    already linked to someone else, and changes nothing.
*/
CREATE PROCEDURE [dbo].[UserSetDiscord]
    @UserID      BIGINT,
    @DiscordID   VARCHAR (32),
    @DiscordName NVARCHAR (64)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @DiscordID IS NOT NULL
       AND EXISTS (SELECT 1 FROM [dbo].[User] WITH (UPDLOCK, HOLDLOCK) WHERE [DiscordID] = @DiscordID AND [UserID] <> @UserID)
    BEGIN
        SELECT CAST(0 AS BIT) AS [Done];
        RETURN;
    END

    UPDATE [dbo].[User]
    SET [DiscordID] = @DiscordID,
        [DiscordName] = CASE WHEN @DiscordID IS NULL THEN NULL ELSE @DiscordName END
    WHERE [UserID] = @UserID;

    SELECT CAST(1 AS BIT) AS [Done];
END
