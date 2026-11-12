/*
    UserCreate
    Creates an account. The first account is always allowed and becomes the admin; later ones need sign-up to be open.
*/
CREATE PROCEDURE [dbo].[UserCreate]
    @Username          NVARCHAR (32),
    @PasswordHash      NVARCHAR (200),
    @SecurityStamp     NVARCHAR (64),
    @AllowRegistration BIT,
    @Outcome           INT OUTPUT    -- 0 created, 1 username taken, 2 sign-up closed
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;

    DECLARE @UserCount INT;
    DECLARE @UserID BIGINT = NULL;

    BEGIN TRANSACTION;

    SELECT @UserCount = COUNT(*)
    FROM [dbo].[User] WITH (UPDLOCK, HOLDLOCK);

    IF @UserCount > 0 AND @AllowRegistration = 0
    BEGIN
        SET @Outcome = 2;
    END
    ELSE IF EXISTS (SELECT 1 FROM [dbo].[User] WHERE [Username] = @Username)
    BEGIN
        SET @Outcome = 1;
    END
    ELSE
    BEGIN
        INSERT INTO [dbo].[User] ([Username], [PasswordHash], [IsAdmin], [SecurityStamp])
        VALUES (@Username, @PasswordHash, CASE WHEN @UserCount = 0 THEN 1 ELSE 0 END, @SecurityStamp);

        SET @UserID = SCOPE_IDENTITY();
        SET @Outcome = 0;
    END

    COMMIT TRANSACTION;

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
