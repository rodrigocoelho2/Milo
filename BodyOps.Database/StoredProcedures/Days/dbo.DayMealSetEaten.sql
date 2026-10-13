/*
    DayMealSetEaten
    Ticks or unticks one meal of a day (the day is created from the plan the first time). @Found = 0 when that day
    has no such meal.
*/
CREATE PROCEDURE [dbo].[DayMealSetEaten]
    @UserID     BIGINT,
    @DayDate    DATE,
    @PlanMealID BIGINT,
    @Eaten      BIT,
    @Found      BIT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;

    DECLARE @DayID BIGINT;

    BEGIN TRANSACTION;

    EXEC [dbo].[DayEnsure] @UserID = @UserID, @DayDate = @DayDate, @DayID = @DayID OUTPUT;

    UPDATE [dbo].[DayMeal]
    SET [Eaten] = @Eaten
    WHERE [DayID] = @DayID
      AND [PlanMealID] = @PlanMealID;

    SET @Found = CASE WHEN @@ROWCOUNT > 0 THEN 1 ELSE 0 END;

    UPDATE [dbo].[Day] SET [UpdatedAt] = SYSUTCDATETIME() WHERE [DayID] = @DayID;

    COMMIT TRANSACTION;
END
