CREATE TABLE [dbo].[WatchWorkout]
(
    [WatchWorkoutID]  BIGINT          IDENTITY (1, 1) NOT NULL,
    [UserID]          BIGINT          NOT NULL,
    [DayDate]         DATE            NOT NULL,
    [StartedAt]       DATETIME2 (0)   NOT NULL,
    [EndedAt]         DATETIME2 (0)   NOT NULL,
    [Kind]            NVARCHAR (60)   NOT NULL CONSTRAINT [DF_WatchWorkout_Kind] DEFAULT (N''),
    [DurationSeconds] INT             NOT NULL,
    [ActiveKcal]      DECIMAL (7, 1)  NULL,
    [AvgBpm]          SMALLINT        NULL,
    [MaxBpm]          SMALLINT        NULL,
    [DistanceKm]      DECIMAL (7, 2)  NULL,
    [HeartRates]      NVARCHAR (1000) NOT NULL CONSTRAINT [DF_WatchWorkout_HeartRates] DEFAULT (N''),
    [PlanExerciseID]  BIGINT          NULL,
    [CreatedAt]       DATETIME2 (0)   NOT NULL CONSTRAINT [DF_WatchWorkout_CreatedAt] DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_WatchWorkout] PRIMARY KEY CLUSTERED ([WatchWorkoutID] ASC),
    CONSTRAINT [UQ_WatchWorkout_Start] UNIQUE NONCLUSTERED ([UserID] ASC, [StartedAt] ASC),
    CONSTRAINT [FK_WatchWorkout_User] FOREIGN KEY ([UserID]) REFERENCES [dbo].[User] ([UserID]) ON DELETE CASCADE,
    CONSTRAINT [CK_WatchWorkout_Duration] CHECK ([DurationSeconds] > 0)
);
GO

CREATE NONCLUSTERED INDEX [IX_WatchWorkout_Day]
    ON [dbo].[WatchWorkout] ([UserID] ASC, [DayDate] ASC);
GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Workouts recorded on an Apple Watch and sent by the iOS Shortcut: times in the app''s time zone, active calories, heart rate (average, maximum and a curve of up to 60 points, comma-separated) and distance. PlanExerciseID is the planned cardio it filled in, if any (no foreign key: plans change, the workout stays).',
    @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'WatchWorkout';
