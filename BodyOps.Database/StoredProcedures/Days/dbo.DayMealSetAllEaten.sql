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

    -- Ticking all keeps any off-plan swap already logged; unticking clears them.
    UPDATE [dbo].[DayMeal]
    SET [Eaten] = @Eaten,
        [SwapName] = CASE WHEN @Eaten = 1 THEN [SwapName] END,
        [SwapKcal] = CASE WHEN @Eaten = 1 THEN [SwapKcal] END,
        [SwapProteinG] = CASE WHEN @Eaten = 1 THEN [SwapProteinG] END
    WHERE [DayID] = @DayID;
    UPDATE [dbo].[Day] SET [UpdatedAt] = SYSUTCDATETIME() WHERE [DayID] = @DayID;

    COMMIT TRANSACTION;
END
