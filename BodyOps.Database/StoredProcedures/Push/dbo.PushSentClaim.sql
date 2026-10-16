/*
    PushSentClaim
    Claims a reminder before it is sent: @Claimed = 1 the first time for that account and key, 0 when it already went
    out. Also forgets claims older than two weeks.
*/
CREATE PROCEDURE [dbo].[PushSentClaim]
    @UserID  BIGINT,
    @SentKey NVARCHAR (80),
    @Claimed BIT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DELETE FROM [dbo].[PushSent] WHERE [UserID] = @UserID AND [SentAt] < DATEADD(DAY, -14, SYSUTCDATETIME());

    INSERT INTO [dbo].[PushSent] ([UserID], [SentKey])
    SELECT @UserID, @SentKey
    WHERE NOT EXISTS (SELECT 1 FROM [dbo].[PushSent] WITH (UPDLOCK, HOLDLOCK) WHERE [UserID] = @UserID AND [SentKey] = @SentKey);

    SET @Claimed = CASE WHEN @@ROWCOUNT > 0 THEN 1 ELSE 0 END;
END
