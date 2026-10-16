/*
    PushSubscriptionList
    The devices with push turned on: one account's (@UserID), or every account's (@UserID NULL, for the reminders).
*/
CREATE PROCEDURE [dbo].[PushSubscriptionList]
    @UserID BIGINT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;

    SELECT s.[PushSubscriptionID],
           s.[UserID],
           s.[Endpoint],
           s.[P256dh],
           s.[Auth],
           s.[Device],
           s.[CreatedAt],
           s.[LastSentAt],
           s.[LastError]
    FROM [dbo].[PushSubscription] s
    WHERE @UserID IS NULL OR s.[UserID] = @UserID
    ORDER BY s.[UserID], s.[CreatedAt] DESC;
END
