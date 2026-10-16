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
    [SwapName]     NVARCHAR (80)  NULL,
    [SwapKcal]     DECIMAL (7, 1) NULL,
    [SwapProteinG] DECIMAL (6, 1) NULL,
    CONSTRAINT [PK_DayMeal] PRIMARY KEY CLUSTERED ([DayMealID] ASC),
    CONSTRAINT [UQ_DayMeal_DayID_PlanMealID] UNIQUE NONCLUSTERED ([DayID] ASC, [PlanMealID] ASC),
    CONSTRAINT [FK_DayMeal_Day] FOREIGN KEY ([DayID]) REFERENCES [dbo].[Day] ([DayID]) ON DELETE CASCADE,
    CONSTRAINT [CK_DayMeal_Swap] CHECK (([SwapKcal] IS NULL OR [SwapKcal] BETWEEN 0 AND 10000) AND ([SwapProteinG] IS NULL OR [SwapProteinG] BETWEEN 0 AND 1000))
);
GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'The meals of a logged day, copied from the plan (with the planned time). PlanMealID has no foreign key on purpose: the row keeps its history after the planned meal is deleted (imported history may use negative ids). SwapName/SwapKcal/SwapProteinG: what was eaten instead of the planned foods (an off-plan meal), NULL when it was eaten as planned.',
    @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'DayMeal';
