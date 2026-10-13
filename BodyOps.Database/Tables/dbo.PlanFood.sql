CREATE TABLE [dbo].[PlanFood]
(
    [PlanFoodID] BIGINT         IDENTITY (1, 1) NOT NULL,
    [PlanMealID] BIGINT         NOT NULL,
    [Name]       NVARCHAR (80)  NOT NULL,
    [Amount]     DECIMAL (8, 2) NOT NULL,
    [Unit]       TINYINT        NOT NULL,
    [SortOrder]  SMALLINT       NOT NULL,
    CONSTRAINT [PK_PlanFood] PRIMARY KEY CLUSTERED ([PlanFoodID] ASC),
    CONSTRAINT [FK_PlanFood_PlanMeal] FOREIGN KEY ([PlanMealID]) REFERENCES [dbo].[PlanMeal] ([PlanMealID]) ON DELETE CASCADE,
    CONSTRAINT [CK_PlanFood_Amount] CHECK ([Amount] >= 0),
    CONSTRAINT [CK_PlanFood_Unit] CHECK ([Unit] IN (1, 2))
);
GO

CREATE NONCLUSTERED INDEX [IX_PlanFood_PlanMealID]
    ON [dbo].[PlanFood] ([PlanMealID] ASC, [SortOrder] ASC);
GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'The foods of a planned meal. Unit: 1 = grams, 2 = pieces.',
    @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'PlanFood';
