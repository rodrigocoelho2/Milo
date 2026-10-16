CREATE TABLE [dbo].[DaySet]
(
    [DayExerciseID] BIGINT   NOT NULL,
    [SetNumber]     TINYINT  NOT NULL,
    [Reps]          SMALLINT NOT NULL,
    [LoadKg]        DECIMAL (6, 2) NULL,
    CONSTRAINT [PK_DaySet] PRIMARY KEY CLUSTERED ([DayExerciseID] ASC, [SetNumber] ASC),
    CONSTRAINT [FK_DaySet_DayExercise] FOREIGN KEY ([DayExerciseID]) REFERENCES [dbo].[DayExercise] ([DayExerciseID]) ON DELETE CASCADE,
    CONSTRAINT [CK_DaySet_SetNumber] CHECK ([SetNumber] BETWEEN 1 AND 20),
    CONSTRAINT [CK_DaySet_Reps] CHECK ([Reps] BETWEEN 0 AND 999),
    CONSTRAINT [CK_DaySet_LoadKg] CHECK ([LoadKg] IS NULL OR [LoadKg] >= 0)
);
GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'The sets done for one exercise on one day: reps and load per set number (1-based), so each set can have its own load (progressive loading). LoadKg NULL = the exercise''s LoadKg (sets logged before per-set loads). Sets not done have no row. Volume = sum of reps × load.',
    @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'DaySet';
