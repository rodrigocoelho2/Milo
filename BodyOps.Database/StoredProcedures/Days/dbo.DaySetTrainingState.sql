/*
    DaySetTrainingState
    Marks a day's session as not marked (0), trained (1) or skipped (2).
*/
CREATE PROCEDURE [dbo].[DaySetTrainingState]
    @UserID        BIGINT,
    @DayDate       DATE,
    @TrainingState TINYINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;

    DECLARE @DayID BIGINT;

    BEGIN TRANSACTION;

    EXEC [dbo].[DayEnsure] @UserID = @UserID, @DayDate = @DayDate, @DayID = @DayID OUTPUT;

    UPDATE [dbo].[Day]
    SET [TrainingState] = @TrainingState,
        [UpdatedAt] = SYSUTCDATETIME()
    WHERE [DayID] = @DayID;

    COMMIT TRANSACTION;
END
