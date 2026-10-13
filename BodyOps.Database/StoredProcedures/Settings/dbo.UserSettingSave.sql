/*
    UserSettingSave
    Creates or replaces the account's settings and returns them.
*/
CREATE PROCEDURE [dbo].[UserSettingSave]
    @UserID       BIGINT,
    @WeightGoalKg DECIMAL (5, 1) = NULL,
    @RestSeconds  SMALLINT       = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;

    BEGIN TRANSACTION;

    UPDATE [dbo].[UserSetting] WITH (UPDLOCK, HOLDLOCK)
    SET [WeightGoalKg] = @WeightGoalKg,
        [RestSeconds] = @RestSeconds,
        [UpdatedAt] = SYSUTCDATETIME()
    WHERE [UserID] = @UserID;

    IF @@ROWCOUNT = 0
    BEGIN
        INSERT INTO [dbo].[UserSetting] ([UserID], [WeightGoalKg], [RestSeconds])
        VALUES (@UserID, @WeightGoalKg, @RestSeconds);
    END

    COMMIT TRANSACTION;

    SELECT s.[UserID],
           s.[WeightGoalKg],
           CAST(s.[RestSeconds] AS INT) AS [RestSeconds],
           s.[UpdatedAt]
    FROM [dbo].[UserSetting] s
    WHERE s.[UserID] = @UserID;
END
