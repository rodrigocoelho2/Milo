/*
    FoodList
    The account's foods, by name.
*/
CREATE PROCEDURE [dbo].[FoodList]
    @UserID BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;

    SELECT f.[FoodID],
           f.[Name],
           f.[NameKey],
           CAST(f.[Basis] AS INT) AS [Basis],
           f.[PieceGrams],
           f.[PieceName],
           f.[Kcal],
           f.[ProteinG],
           f.[CarbsG],
           f.[FatG],
           CAST(f.[Source] AS INT) AS [Source],
           f.[SourceRef],
           f.[UpdatedAt]
    FROM [dbo].[Food] f
    WHERE f.[UserID] = @UserID
    ORDER BY f.[NameKey];
END
