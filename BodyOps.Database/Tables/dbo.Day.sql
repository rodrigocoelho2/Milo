CREATE TABLE [dbo].[Day]
(
    [DayID]         BIGINT        IDENTITY (1, 1) NOT NULL,
    [UserID]        BIGINT        NOT NULL,
    [DayDate]       DATE          NOT NULL,
    [TrainingState] TINYINT       NOT NULL CONSTRAINT [DF_Day_TrainingState] DEFAULT (0),
    [UpdatedAt]     DATETIME2 (3) NOT NULL CONSTRAINT [DF_Day_UpdatedAt] DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_Day] PRIMARY KEY CLUSTERED ([DayID] ASC),
    CONSTRAINT [UQ_Day_UserID_DayDate] UNIQUE NONCLUSTERED ([UserID] ASC, [DayDate] ASC),
    CONSTRAINT [FK_Day_User] FOREIGN KEY ([UserID]) REFERENCES [dbo].[User] ([UserID]) ON DELETE CASCADE,
    CONSTRAINT [CK_Day_TrainingState] CHECK ([TrainingState] IN (0, 1, 2))
);
GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'A logged calendar day. Created the first time something is ticked or logged on that date, with a copy of that weekday''s plan in dbo.DayMeal / dbo.DayExercise, so editing the plan later never rewrites past weeks. TrainingState: 0 = not marked, 1 = trained, 2 = skipped.',
    @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'Day';
