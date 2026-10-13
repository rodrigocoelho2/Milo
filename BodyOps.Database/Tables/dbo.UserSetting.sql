CREATE TABLE [dbo].[UserSetting]
(
    [UserID]       BIGINT        NOT NULL,
    [WeightGoalKg] DECIMAL (5, 1) NULL,
    [UpdatedAt]    DATETIME2 (3) NOT NULL CONSTRAINT [DF_UserSetting_UpdatedAt] DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_UserSetting] PRIMARY KEY CLUSTERED ([UserID] ASC),
    CONSTRAINT [FK_UserSetting_User] FOREIGN KEY ([UserID]) REFERENCES [dbo].[User] ([UserID]) ON DELETE CASCADE,
    CONSTRAINT [CK_UserSetting_WeightGoalKg] CHECK ([WeightGoalKg] IS NULL OR ([WeightGoalKg] >= 20 AND [WeightGoalKg] <= 400))
);
GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Per-account settings (weight goal). One row per account, created on first save.',
    @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'UserSetting';
