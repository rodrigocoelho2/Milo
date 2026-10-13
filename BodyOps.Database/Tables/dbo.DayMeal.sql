CREATE TABLE [dbo].[DayMeal]
(
    [DayMealID]  BIGINT         IDENTITY (1, 1) NOT NULL,
    [DayID]      BIGINT         NOT NULL,
    [PlanMealID] BIGINT         NOT NULL,
    [Name]       NVARCHAR (80)  NOT NULL,
    [Foods]      NVARCHAR (2000) NOT NULL CONSTRAINT [DF_DayMeal_Foods] DEFAULT (N''),
    [SortOrder]  SMALLINT       NOT NULL,
    [Eaten]      BIT            NOT NULL CONSTRAINT [DF_DayMeal_Eaten] DEFAULT (0),
    [MealTime]   TIME (0)       NULL,
    CONSTRAINT [PK_DayMeal] PRIMARY KEY CLUSTERED ([DayMealID] ASC),
    CONSTRAINT [UQ_DayMeal_DayID_PlanMealID] UNIQUE NONCLUSTERED ([DayID] ASC, [PlanMealID] ASC),
    CONSTRAINT [FK_DayMeal_Day] FOREIGN KEY ([DayID]) REFERENCES [dbo].[Day] ([DayID]) ON DELETE CASCADE
);
GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'The meals of a logged day, copied from the plan (with the planned time). PlanMealID has no foreign key on purpose: the row keeps its history after the planned meal is deleted (imported history may use negative ids).',
    @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'DayMeal';
