/*
    FoodImport
    Adds the foods of a backup, as JSON:
        [{"name":"Oats","nameKey":"oats","basis":1,"pieceGrams":null,"pieceName":null,"kcal":372,"proteinG":13.5,
          "carbsG":59,"fatG":7,"source":2,"sourceRef":"u08120"}, …]
    A food whose name the account already has is left as it is.
*/
CREATE PROCEDURE [dbo].[FoodImport]
    @UserID BIGINT,
    @Json   NVARCHAR (MAX)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    INSERT INTO [dbo].[Food] ([UserID], [Name], [NameKey], [Basis], [PieceGrams], [PieceName], [Kcal], [ProteinG], [CarbsG], [FatG], [Source], [SourceRef])
    SELECT @UserID, f.[Name], f.[NameKey],
           CASE WHEN f.[Basis] = 2 THEN 2 ELSE 1 END,
           CASE WHEN f.[PieceGrams] > 0 THEN f.[PieceGrams] END,
           f.[PieceName],
           CASE WHEN f.[Kcal] BETWEEN 0 AND 10000 THEN f.[Kcal] END,
           CASE WHEN f.[ProteinG] BETWEEN 0 AND 1000 THEN f.[ProteinG] END,
           CASE WHEN f.[CarbsG] BETWEEN 0 AND 1000 THEN f.[CarbsG] END,
           CASE WHEN f.[FatG] BETWEEN 0 AND 1000 THEN f.[FatG] END,
           CASE WHEN f.[Source] BETWEEN 1 AND 3 THEN f.[Source] ELSE 1 END,
           f.[SourceRef]
    FROM (
        SELECT j.*, ROW_NUMBER() OVER (PARTITION BY j.[NameKey] ORDER BY (SELECT NULL)) AS [Copy]
        FROM OPENJSON(@Json) WITH (
            [Name]       NVARCHAR (80)  '$.name',
            [NameKey]    NVARCHAR (80)  '$.nameKey',
            [Basis]      TINYINT        '$.basis',
            [PieceGrams] DECIMAL (7, 1) '$.pieceGrams',
            [PieceName]  NVARCHAR (20)  '$.pieceName',
            [Kcal]       DECIMAL (7, 1) '$.kcal',
            [ProteinG]   DECIMAL (6, 1) '$.proteinG',
            [CarbsG]     DECIMAL (6, 1) '$.carbsG',
            [FatG]       DECIMAL (6, 1) '$.fatG',
            [Source]     TINYINT        '$.source',
            [SourceRef]  NVARCHAR (40)  '$.sourceRef') j
        WHERE j.[Name] IS NOT NULL AND j.[NameKey] IS NOT NULL AND LEN(j.[NameKey]) > 0
    ) f
    WHERE f.[Copy] = 1
      AND NOT EXISTS (SELECT 1 FROM [dbo].[Food] x WHERE x.[UserID] = @UserID AND x.[NameKey] = f.[NameKey]);
END
