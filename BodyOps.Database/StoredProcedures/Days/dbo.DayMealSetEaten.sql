/*
    DayMealSetEaten
    Ticks or unticks one meal of a day (the day is created from the plan the first time). @Found = 0 when that day
    has no such meal. With @SwapName the meal was eaten off plan (something else, with its calories and protein when
    known); without, it was eaten as planned. Unticking clears the swap.
*/
CREATE PROCEDURE [dbo].[DayMealSetEaten]
    @UserID     BIGINT,
    @DayDate    DATE,
    @PlanMealID BIGINT,
    @Eaten        BIT,
    @SwapName     NVARCHAR (80) = NULL,
    @SwapKcal     DECIMAL (7, 1) = NULL,
    @SwapProteinG DECIMAL (6, 1) = NULL,
    @Found        BIT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;

    DECLARE @DayID BIGINT;

    BEGIN TRANSACTION;

    EXEC [dbo].[DayEnsure] @UserID = @UserID, @DayDate = @DayDate, @DayID = @DayID OUTPUT;

    UPDATE [dbo].[DayMeal]
    SET [Eaten] = @Eaten,
        [SwapName] = CASE WHEN @Eaten = 1 THEN @SwapName END,
        [SwapKcal] = CASE WHEN @Eaten = 1 AND @SwapName IS NOT NULL THEN @SwapKcal END,
        [SwapProteinG] = CASE WHEN @Eaten = 1 AND @SwapName IS NOT NULL THEN @SwapProteinG END
    WHERE [DayID] = @DayID
      AND [PlanMealID] = @PlanMealID;

    SET @Found = CASE WHEN @@ROWCOUNT > 0 THEN 1 ELSE 0 END;

    UPDATE [dbo].[Day] SET [UpdatedAt] = SYSUTCDATETIME() WHERE [DayID] = @DayID;

    COMMIT TRANSACTION;
END
