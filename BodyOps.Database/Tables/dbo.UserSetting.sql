CREATE TABLE [dbo].[UserSetting]
(
    [UserID]       BIGINT        NOT NULL,
    [WeightGoalKg] DECIMAL (5, 1) NULL,
    [UpdatedAt]    DATETIME2 (3) NOT NULL CONSTRAINT [DF_UserSetting_UpdatedAt] DEFAULT (SYSUTCDATETIME()),
    [RestSeconds]  SMALLINT      NULL,
    CONSTRAINT [PK_UserSetting] PRIMARY KEY CLUSTERED ([UserID] ASC),
    CONSTRAINT [FK_UserSetting_User] FOREIGN KEY ([UserID]) REFERENCES [dbo].[User] ([UserID]) ON DELETE CASCADE,
    CONSTRAINT [CK_UserSetting_WeightGoalKg] CHECK ([WeightGoalKg] IS NULL OR ([WeightGoalKg] >= 20 AND [WeightGoalKg] <= 400)),
    CONSTRAINT [CK_UserSetting_RestSeconds] CHECK ([RestSeconds] IS NULL OR ([RestSeconds] >= 15 AND [RestSeconds] <= 900))
);
GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Per-account settings: weight goal and the rest between sets (seconds; NULL = the default). One row per account, created on first save.',
    @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'UserSetting';
