CREATE TABLE [dbo].[Food]
(
    [FoodID]     BIGINT         IDENTITY (1, 1) NOT NULL,
    [UserID]     BIGINT         NOT NULL,
    [Name]       NVARCHAR (80)  NOT NULL,
    [NameKey]    NVARCHAR (80)  NOT NULL,
    [Basis]      TINYINT        NOT NULL CONSTRAINT [DF_Food_Basis] DEFAULT (1),
    [PieceGrams] DECIMAL (7, 1) NULL,
    [PieceName]  NVARCHAR (20)  NULL,
    [Kcal]       DECIMAL (7, 1) NULL,
    [ProteinG]   DECIMAL (6, 1) NULL,
    [CarbsG]     DECIMAL (6, 1) NULL,
    [FatG]       DECIMAL (6, 1) NULL,
    [Source]     TINYINT        NOT NULL CONSTRAINT [DF_Food_Source] DEFAULT (1),
    [SourceRef]  NVARCHAR (40)  NULL,
    [UpdatedAt]  DATETIME2 (0)  NOT NULL CONSTRAINT [DF_Food_UpdatedAt] DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_Food] PRIMARY KEY CLUSTERED ([FoodID] ASC),
    CONSTRAINT [UQ_Food_UserID_NameKey] UNIQUE NONCLUSTERED ([UserID] ASC, [NameKey] ASC),
    CONSTRAINT [FK_Food_User] FOREIGN KEY ([UserID]) REFERENCES [dbo].[User] ([UserID]) ON DELETE CASCADE,
    CONSTRAINT [CK_Food_Basis] CHECK ([Basis] IN (1, 2)),
    CONSTRAINT [CK_Food_Source] CHECK ([Source] BETWEEN 1 AND 3),
    CONSTRAINT [CK_Food_Values] CHECK (([Kcal] IS NULL OR [Kcal] BETWEEN 0 AND 10000) AND ([ProteinG] IS NULL OR [ProteinG] BETWEEN 0 AND 1000)
        AND ([CarbsG] IS NULL OR [CarbsG] BETWEEN 0 AND 1000) AND ([FatG] IS NULL OR [FatG] BETWEEN 0 AND 1000)
        AND ([PieceGrams] IS NULL OR [PieceGrams] > 0))
);
GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'An account''s foods with their label values, matched to the plan''s foods by name (NameKey = the cleaned name in lower case). Basis: 1 = values per 100 g, 2 = values per piece. PieceGrams: the weight of one piece/serving, when known (lets grams and pieces convert). Source: 1 = typed by the user, 2 = Milo''s built-in list (SourceRef = its id), 3 = Open Food Facts (SourceRef = the barcode).',
    @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'Food';
