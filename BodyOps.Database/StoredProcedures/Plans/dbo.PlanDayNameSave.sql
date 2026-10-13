/*
    PlanDayNameSave
    Names a weekday's session in the plan ("Upper body"). An empty @Name removes the name.
*/
CREATE PROCEDURE [dbo].[PlanDayNameSave]
    @UserID  BIGINT,
    @Weekday TINYINT,
    @Name    NVARCHAR (40)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;

    BEGIN TRANSACTION;

    IF @Name IS NULL OR LEN(@Name) = 0
    BEGIN
        DELETE FROM [dbo].[PlanDay] WHERE [UserID] = @UserID AND [Weekday] = @Weekday;
    END
    ELSE
    BEGIN
        UPDATE [dbo].[PlanDay] WITH (UPDLOCK, HOLDLOCK)
        SET [Name] = @Name
        WHERE [UserID] = @UserID AND [Weekday] = @Weekday;

        IF @@ROWCOUNT = 0
        BEGIN
            INSERT INTO [dbo].[PlanDay] ([UserID], [Weekday], [Name])
            VALUES (@UserID, @Weekday, @Name);
        END
    END

    COMMIT TRANSACTION;
END
