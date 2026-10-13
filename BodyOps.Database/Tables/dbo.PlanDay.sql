CREATE TABLE [dbo].[PlanDay]
(
    [UserID]  BIGINT        NOT NULL,
    [Weekday] TINYINT       NOT NULL,
    [Name]    NVARCHAR (40) NOT NULL,
    CONSTRAINT [PK_PlanDay] PRIMARY KEY CLUSTERED ([UserID] ASC, [Weekday] ASC),
    CONSTRAINT [FK_PlanDay_User] FOREIGN KEY ([UserID]) REFERENCES [dbo].[User] ([UserID]) ON DELETE CASCADE,
    CONSTRAINT [CK_PlanDay_Weekday] CHECK ([Weekday] BETWEEN 1 AND 7)
);
GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'The name of each weekday''s session in the plan ("Upper body", "Legs", "Rest"). Weekdays without a row have no name.',
    @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'PlanDay';
