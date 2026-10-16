CREATE TABLE [dbo].[PlanExercise]
(
    [PlanExerciseID] BIGINT         IDENTITY (1, 1) NOT NULL,
    [UserID]         BIGINT         NOT NULL,
    [Weekday]        TINYINT        NOT NULL,
    [ExerciseType]   TINYINT        NOT NULL,
    [Name]           NVARCHAR (100) NOT NULL,
    [Detail]         NVARCHAR (60)  NOT NULL CONSTRAINT [DF_PlanExercise_Detail] DEFAULT (N''),
    [Notes]          NVARCHAR (200) NOT NULL CONSTRAINT [DF_PlanExercise_Notes] DEFAULT (N''),
    [Muscles]        NVARCHAR (60)  NULL,
    [CardioKind]     TINYINT        NULL,
    [ProgressStepKg] DECIMAL (5, 2) NULL,
    [SortOrder]      SMALLINT       NOT NULL,
    CONSTRAINT [PK_PlanExercise] PRIMARY KEY CLUSTERED ([PlanExerciseID] ASC),
    CONSTRAINT [FK_PlanExercise_User] FOREIGN KEY ([UserID]) REFERENCES [dbo].[User] ([UserID]) ON DELETE CASCADE,
    CONSTRAINT [CK_PlanExercise_Weekday] CHECK ([Weekday] BETWEEN 1 AND 7),
    CONSTRAINT [CK_PlanExercise_ExerciseType] CHECK ([ExerciseType] IN (1, 2)),
    CONSTRAINT [CK_PlanExercise_CardioKind] CHECK ([CardioKind] IS NULL OR [CardioKind] BETWEEN 1 AND 9),
    CONSTRAINT [CK_PlanExercise_ProgressStepKg] CHECK ([ProgressStepKg] IS NULL OR [ProgressStepKg] BETWEEN 0.5 AND 20)
);
GO

CREATE NONCLUSTERED INDEX [IX_PlanExercise_UserID_Weekday]
    ON [dbo].[PlanExercise] ([UserID] ASC, [Weekday] ASC, [SortOrder] ASC);
GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'The weekly training plan. ExerciseType: 1 = strength, 2 = cardio. Detail is the plan as typed ("3x12", "20 km"). Muscles: comma-separated muscle numbers chosen by hand, or NULL to detect them from the name. CardioKind (cardio only): 1 bike, 2 treadmill, 3 run, 4 walk, 5 elliptical, 6 rowing, 7 stairs, 8 swim, 9 other; NULL = work it out from the name. ProgressStepKg (strength with a rep range such as 3x8-12): the weight added once every set reaches the top of the range; NULL = 2.5 kg.',
    @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'PlanExercise';
