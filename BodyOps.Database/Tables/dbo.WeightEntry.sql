CREATE TABLE [dbo].[WeightEntry]
(
    [UserID]    BIGINT        NOT NULL,
    [EntryDate] DATE          NOT NULL,
    [WeightKg]  DECIMAL (5, 1) NOT NULL,
    [UpdatedAt] DATETIME2 (3) NOT NULL CONSTRAINT [DF_WeightEntry_UpdatedAt] DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_WeightEntry] PRIMARY KEY CLUSTERED ([UserID] ASC, [EntryDate] ASC),
    CONSTRAINT [FK_WeightEntry_User] FOREIGN KEY ([UserID]) REFERENCES [dbo].[User] ([UserID]) ON DELETE CASCADE,
    CONSTRAINT [CK_WeightEntry_WeightKg] CHECK ([WeightKg] >= 20 AND [WeightKg] <= 400)
);
GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Body weigh-ins: at most one per account per day.',
    @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'WeightEntry';
