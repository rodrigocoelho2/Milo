/*
    UserGet
    One account by its identifier.
*/
CREATE PROCEDURE [dbo].[UserGet]
    @UserID BIGINT
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
    WHERE u.[UserID] = @UserID;
END
