/*
    FoodDelete
    Removes one of the account's foods (the plan keeps the food's name; it just has no values any more).
    @Found = 0 when the account has no such food.
*/
CREATE PROCEDURE [dbo].[FoodDelete]
    @UserID BIGINT,
    @FoodID BIGINT,
    @Found  BIT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DELETE FROM [dbo].[Food] WHERE [FoodID] = @FoodID AND [UserID] = @UserID;
    SET @Found = CASE WHEN @@ROWCOUNT > 0 THEN 1 ELSE 0 END;
END
