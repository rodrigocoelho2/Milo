/*
    FoodSave
    Adds or changes one food. With @FoodID it changes that food (it must belong to the account); without, a food with
    the same name is changed, or a new one added (so adding "Oats" twice keeps one).
    @Result: 1 = saved, 0 = @FoodID not found, 2 = another food already has that name.
    Returns the saved food (same shape as dbo.FoodList).
*/
CREATE PROCEDURE [dbo].[FoodSave]
    @UserID     BIGINT,
    @FoodID     BIGINT = NULL,
    @Name       NVARCHAR (80),
    @NameKey    NVARCHAR (80),
    @Basis      TINYINT,
    @PieceGrams DECIMAL (7, 1) = NULL,
    @PieceName  NVARCHAR (20) = NULL,
    @Kcal       DECIMAL (7, 1) = NULL,
    @ProteinG   DECIMAL (6, 1) = NULL,
    @CarbsG     DECIMAL (6, 1) = NULL,
    @FatG       DECIMAL (6, 1) = NULL,
    @Source     TINYINT = 1,
    @SourceRef  NVARCHAR (40) = NULL,
    @Result     INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;

    BEGIN TRANSACTION;

    DECLARE @SameName BIGINT = (SELECT [FoodID] FROM [dbo].[Food] WITH (UPDLOCK) WHERE [UserID] = @UserID AND [NameKey] = @NameKey);

    IF @FoodID IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [dbo].[Food] WITH (UPDLOCK) WHERE [FoodID] = @FoodID AND [UserID] = @UserID)
    BEGIN
        SET @Result = 0;
        COMMIT TRANSACTION;
        RETURN;
    END

    IF @FoodID IS NOT NULL AND @SameName IS NOT NULL AND @SameName <> @FoodID
    BEGIN
        SET @Result = 2;
        COMMIT TRANSACTION;
        RETURN;
    END

    SET @FoodID = ISNULL(@FoodID, @SameName);

    IF @FoodID IS NULL
    BEGIN
        INSERT INTO [dbo].[Food] ([UserID], [Name], [NameKey], [Basis], [PieceGrams], [PieceName], [Kcal], [ProteinG], [CarbsG], [FatG], [Source], [SourceRef])
        VALUES (@UserID, @Name, @NameKey, @Basis, @PieceGrams, @PieceName, @Kcal, @ProteinG, @CarbsG, @FatG, @Source, @SourceRef);
        SET @FoodID = SCOPE_IDENTITY();
    END
    ELSE
    BEGIN
        UPDATE [dbo].[Food]
        SET [Name] = @Name,
            [NameKey] = @NameKey,
            [Basis] = @Basis,
            [PieceGrams] = @PieceGrams,
            [PieceName] = @PieceName,
            [Kcal] = @Kcal,
            [ProteinG] = @ProteinG,
            [CarbsG] = @CarbsG,
            [FatG] = @FatG,
            [Source] = @Source,
            [SourceRef] = @SourceRef,
            [UpdatedAt] = SYSUTCDATETIME()
        WHERE [FoodID] = @FoodID;
    END

    SET @Result = 1;
    COMMIT TRANSACTION;

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
    WHERE f.[FoodID] = @FoodID;
END
