CREATE TABLE [dbo].[PlanMeal]
(
    [PlanMealID] BIGINT        IDENTITY (1, 1) NOT NULL,
    [UserID]     BIGINT        NOT NULL,
    [Weekday]    TINYINT       NOT NULL,
    [Name]       NVARCHAR (80) NOT NULL,
    [SortOrder]  SMALLINT      NOT NULL,
    [MealTime]   TIME (0)      NULL,
    CONSTRAINT [PK_PlanMeal] PRIMARY KEY CLUSTERED ([PlanMealID] ASC),
    CONSTRAINT [FK_PlanMeal_User] FOREIGN KEY ([UserID]) REFERENCES [dbo].[User] ([UserID]) ON DELETE CASCADE,
    CONSTRAINT [CK_PlanMeal_Weekday] CHECK ([Weekday] BETWEEN 1 AND 7)
);
GO

CREATE NONCLUSTERED INDEX [IX_PlanMeal_UserID_Weekday]
    ON [dbo].[PlanMeal] ([UserID] ASC, [Weekday] ASC, [SortOrder] ASC);
GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'The weekly diet plan: the meals of each weekday (1 = Monday … 7 = Sunday), in order. MealTime is when the meal is usually eaten (optional), for the Today timeline.',
    @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'PlanMeal';
