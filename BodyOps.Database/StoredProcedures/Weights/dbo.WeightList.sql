/*
    WeightList
    Every weigh-in of the account, oldest first.
*/
CREATE PROCEDURE [dbo].[WeightList]
    @UserID BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;

    SELECT w.[EntryDate],
           w.[WeightKg]
    FROM [dbo].[WeightEntry] w
    WHERE w.[UserID] = @UserID
    ORDER BY w.[EntryDate];
END
