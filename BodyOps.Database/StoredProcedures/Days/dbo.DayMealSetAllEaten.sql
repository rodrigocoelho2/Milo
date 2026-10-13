/*
    DayMealSetAllEaten
    Ticks or unticks every meal of a day (the day is created from the plan the first time).
*/
CREATE PROCEDURE [dbo].[DayMealSetAllEaten]
    @UserID  BIGINT,
    @DayDate DATE,
    @Eaten   BIT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;

    DECLARE @DayID BIGINT;

    BEGIN TRANSACTION;

    EXEC [dbo].[DayEnsure] @UserID = @UserID, @DayDate = @DayDate, @DayID = @DayID OUTPUT;

    UPDATE [dbo].[DayMeal] SET [Eaten] = @Eaten WHERE [DayID] = @DayID;
    UPDATE [dbo].[Day] SET [UpdatedAt] = SYSUTCDATETIME() WHERE [DayID] = @DayID;

    COMMIT TRANSACTION;
END
