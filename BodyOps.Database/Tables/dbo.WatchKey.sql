CREATE TABLE [dbo].[WatchKey]
(
    [UserID]     BIGINT         NOT NULL,
    [KeyHash]    BINARY (32)    NOT NULL,
    [KeyHint]    NVARCHAR (24)  NOT NULL,
    [CreatedAt]  DATETIME2 (0)  NOT NULL CONSTRAINT [DF_WatchKey_CreatedAt] DEFAULT (SYSUTCDATETIME()),
    [LastUsedAt] DATETIME2 (0)  NULL,
    [LastError]  NVARCHAR (200) NOT NULL CONSTRAINT [DF_WatchKey_LastError] DEFAULT (N''),
    CONSTRAINT [PK_WatchKey] PRIMARY KEY CLUSTERED ([UserID] ASC),
    CONSTRAINT [UQ_WatchKey_KeyHash] UNIQUE NONCLUSTERED ([KeyHash] ASC),
    CONSTRAINT [FK_WatchKey_User] FOREIGN KEY ([UserID]) REFERENCES [dbo].[User] ([UserID]) ON DELETE CASCADE
);
GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'The personal key an iOS Shortcut uses to send Apple Watch workouts and weigh-ins from Apple Health. Only its SHA-256 is kept; one key per account, a new one replaces the old.',
    @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'WatchKey';
