CREATE TABLE [dbo].[PushSubscription]
(
    [PushSubscriptionID] BIGINT          IDENTITY (1, 1) NOT NULL,
    [UserID]             BIGINT          NOT NULL,
    [Endpoint]           NVARCHAR (800)  NOT NULL,
    [EndpointHash]       AS CAST(HASHBYTES('SHA2_256', [Endpoint]) AS BINARY (32)) PERSISTED,
    [P256dh]             NVARCHAR (200)  NOT NULL,
    [Auth]               NVARCHAR (100)  NOT NULL,
    [Device]             NVARCHAR (60)   NOT NULL CONSTRAINT [DF_PushSubscription_Device] DEFAULT (N''),
    [CreatedAt]          DATETIME2 (0)   NOT NULL CONSTRAINT [DF_PushSubscription_CreatedAt] DEFAULT (SYSUTCDATETIME()),
    [LastSentAt]         DATETIME2 (0)   NULL,
    [LastError]          NVARCHAR (200)  NOT NULL CONSTRAINT [DF_PushSubscription_LastError] DEFAULT (N''),
    CONSTRAINT [PK_PushSubscription] PRIMARY KEY CLUSTERED ([PushSubscriptionID] ASC),
    CONSTRAINT [UQ_PushSubscription_EndpointHash] UNIQUE NONCLUSTERED ([EndpointHash] ASC),
    CONSTRAINT [FK_PushSubscription_User] FOREIGN KEY ([UserID]) REFERENCES [dbo].[User] ([UserID]) ON DELETE CASCADE
);
GO

CREATE NONCLUSTERED INDEX [IX_PushSubscription_UserID]
    ON [dbo].[PushSubscription] ([UserID] ASC);
GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'The phones and computers that turned push reminders on (Web Push): the browser''s push service endpoint and its encryption keys. One row per browser; signing in with another account on the same browser moves it there.',
    @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'PushSubscription';
