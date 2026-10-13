CREATE TABLE [dbo].[DayExercise]
(
    [DayExerciseID]  BIGINT         IDENTITY (1, 1) NOT NULL,
    [DayID]          BIGINT         NOT NULL,
    [PlanExerciseID] BIGINT         NOT NULL,
    [ExerciseType]   TINYINT        NOT NULL,
    [Name]           NVARCHAR (100) NOT NULL,
    [NameKey]        NVARCHAR (100) NOT NULL,
    [Detail]         NVARCHAR (60)  NOT NULL CONSTRAINT [DF_DayExercise_Detail] DEFAULT (N''),
    [Notes]          NVARCHAR (200) NOT NULL CONSTRAINT [DF_DayExercise_Notes] DEFAULT (N''),
    [Muscles]        NVARCHAR (60)  NULL,
    [SortOrder]      SMALLINT       NOT NULL,
    [LoadKg]         DECIMAL (6, 2) NULL,
    [CardioMinutes]  DECIMAL (6, 1) NULL,
    [CardioKm]       DECIMAL (6, 2) NULL,
    CONSTRAINT [PK_DayExercise] PRIMARY KEY CLUSTERED ([DayExerciseID] ASC),
    CONSTRAINT [UQ_DayExercise_DayID_PlanExerciseID] UNIQUE NONCLUSTERED ([DayID] ASC, [PlanExerciseID] ASC),
    CONSTRAINT [FK_DayExercise_Day] FOREIGN KEY ([DayID]) REFERENCES [dbo].[Day] ([DayID]) ON DELETE CASCADE,
    CONSTRAINT [CK_DayExercise_ExerciseType] CHECK ([ExerciseType] IN (1, 2)),
    CONSTRAINT [CK_DayExercise_LoadKg] CHECK ([LoadKg] IS NULL OR [LoadKg] >= 0)
);
GO

CREATE NONCLUSTERED INDEX [IX_DayExercise_NameKey]
    ON [dbo].[DayExercise] ([NameKey] ASC)
    INCLUDE ([DayID], [LoadKg]);
GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'The exercises of a logged day, copied from the plan, with what was lifted (LoadKg) or ridden/run (CardioMinutes, CardioKm). NameKey is the name in lower case with single spaces: it links the same exercise across days for history and records.',
    @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'DayExercise';
