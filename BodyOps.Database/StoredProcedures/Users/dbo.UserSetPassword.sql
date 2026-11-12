/*
    UserSetPassword
    Sets a new password and a new security stamp, which signs the account out on every other device.
*/
CREATE PROCEDURE [dbo].[UserSetPassword]
    @UserID        BIGINT,
    @PasswordHash  NVARCHAR (200),
    @SecurityStamp NVARCHAR (64)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;

    UPDATE [dbo].[User]
    SET [PasswordHash] = @PasswordHash,
        [SecurityStamp] = @SecurityStamp
    WHERE [UserID] = @UserID;

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
