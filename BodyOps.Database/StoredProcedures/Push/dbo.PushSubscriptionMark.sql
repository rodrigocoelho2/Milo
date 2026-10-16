/*
    PushSubscriptionMark
    Records how the last push to a device went: delivered (@LastError empty, LastSentAt = now) or the error.
*/
CREATE PROCEDURE [dbo].[PushSubscriptionMark]
    @PushSubscriptionID BIGINT,
    @LastError          NVARCHAR (200)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE [dbo].[PushSubscription]
    SET [LastError] = ISNULL(@LastError, N''),
        [LastSentAt] = CASE WHEN ISNULL(@LastError, N'') = N'' THEN SYSUTCDATETIME() ELSE [LastSentAt] END
    WHERE [PushSubscriptionID] = @PushSubscriptionID;
END
