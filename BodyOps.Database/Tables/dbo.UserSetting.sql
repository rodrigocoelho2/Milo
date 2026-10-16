CREATE TABLE [dbo].[UserSetting]
(
    [UserID]         BIGINT        NOT NULL,
    [WeightGoalKg]   DECIMAL (5, 1) NULL,
    [UpdatedAt]      DATETIME2 (3) NOT NULL CONSTRAINT [DF_UserSetting_UpdatedAt] DEFAULT (SYSUTCDATETIME()),
    [RestSeconds]    SMALLINT      NULL,
    [WeighEveryDays] TINYINT       NULL,
    [RemindMeals]    BIT           NULL,
    [RemindWorkout]  BIT           NULL,
    [WorkoutTime]    TIME (0)      NULL,
    [RemindWeighIn]  BIT           NULL,
    [WeighInTime]    TIME (0)      NULL,
    [RemindStreak]   BIT           NULL,
    [StreakTime]     TIME (0)      NULL,
    CONSTRAINT [PK_UserSetting] PRIMARY KEY CLUSTERED ([UserID] ASC),
    CONSTRAINT [FK_UserSetting_User] FOREIGN KEY ([UserID]) REFERENCES [dbo].[User] ([UserID]) ON DELETE CASCADE,
    CONSTRAINT [CK_UserSetting_WeightGoalKg] CHECK ([WeightGoalKg] IS NULL OR ([WeightGoalKg] >= 20 AND [WeightGoalKg] <= 400)),
    CONSTRAINT [CK_UserSetting_RestSeconds] CHECK ([RestSeconds] IS NULL OR ([RestSeconds] >= 15 AND [RestSeconds] <= 900)),
    CONSTRAINT [CK_UserSetting_WeighEveryDays] CHECK ([WeighEveryDays] IS NULL OR ([WeighEveryDays] >= 1 AND [WeighEveryDays] <= 60))
);
GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Per-account settings, one row per account (created on first save). NULL means the default: no weight goal, 90 s rest, weigh-in every day, every reminder on, workout reminder 18:00, weigh-in reminder 08:00, streak reminder 21:00. WeighEveryDays: how often the account weighs in (1–60 days). Remind*: which push reminders the account wants (each device turns push on for itself).',
    @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'UserSetting';
