/*
    UserGetByDiscord
    The account a Discord account is linked to (none: no rows).
*/
CREATE PROCEDURE [dbo].[UserGetByDiscord]
    @DiscordID VARCHAR (32)
AS
BEGIN
    SET NOCOUNT ON;
    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;

    SELECT u.[UserID],
           u.[UserUniqueID],
           u.[Username],
           u.[PasswordHash],
           u.[IsAdmin],
           u.[SecurityStamp],
           u.[CreatedAt],
           u.[LastLoginAt],
           u.[DiscordID],
           u.[DiscordName]
    FROM [dbo].[User] u
    WHERE u.[DiscordID] = @DiscordID;
END
