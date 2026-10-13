CREATE TABLE [dbo].[DaySet]
(
    [DayExerciseID] BIGINT   NOT NULL,
    [SetNumber]     TINYINT  NOT NULL,
    [Reps]          SMALLINT NOT NULL,
    CONSTRAINT [PK_DaySet] PRIMARY KEY CLUSTERED ([DayExerciseID] ASC, [SetNumber] ASC),
    CONSTRAINT [FK_DaySet_DayExercise] FOREIGN KEY ([DayExerciseID]) REFERENCES [dbo].[DayExercise] ([DayExerciseID]) ON DELETE CASCADE,
    CONSTRAINT [CK_DaySet_SetNumber] CHECK ([SetNumber] BETWEEN 1 AND 20),
    CONSTRAINT [CK_DaySet_Reps] CHECK ([Reps] BETWEEN 0 AND 999)
);
GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'The sets done for one exercise on one day: reps per set number (1-based). Sets not done have no row. Volume = sum of reps × the exercise''s LoadKg.',
    @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'DaySet';
