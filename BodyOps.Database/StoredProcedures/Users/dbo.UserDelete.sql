/*
    UserDelete
    Deletes an account with its plan, logged days, weigh-ins and settings (the foreign keys cascade).
    If that leaves no admin, the oldest remaining account becomes the admin.
*/
CREATE PROCEDURE [dbo].[UserDelete]
    @UserID  BIGINT,
    @Deleted BIT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;

    BEGIN TRANSACTION;

    DELETE FROM [dbo].[User] WHERE [UserID] = @UserID;
    SET @Deleted = CASE WHEN @@ROWCOUNT > 0 THEN 1 ELSE 0 END;

    -- Never leave the server without an admin: the oldest remaining account takes over.
    IF NOT EXISTS (SELECT 1 FROM [dbo].[User] WHERE [IsAdmin] = 1)
    BEGIN
        UPDATE [dbo].[User]
        SET [IsAdmin] = 1
        WHERE [UserID] = (SELECT MIN([UserID]) FROM [dbo].[User]);
    END

    COMMIT TRANSACTION;
END
