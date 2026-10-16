/*
    PushSubscriptionDelete
    Removes a device: one of the account's by id (@UserID and @PushSubscriptionID), or by endpoint (@Endpoint, when
    the push service says the browser is gone). @Found = 0 when nothing matched.
*/
CREATE PROCEDURE [dbo].[PushSubscriptionDelete]
    @UserID             BIGINT = NULL,
    @PushSubscriptionID BIGINT = NULL,
    @Endpoint           NVARCHAR (800) = NULL,
    @Found              BIT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DELETE FROM [dbo].[PushSubscription]
    WHERE (@PushSubscriptionID IS NOT NULL AND [PushSubscriptionID] = @PushSubscriptionID AND [UserID] = @UserID)
       OR (@Endpoint IS NOT NULL AND [EndpointHash] = CAST(HASHBYTES('SHA2_256', @Endpoint) AS BINARY (32)));

    SET @Found = CASE WHEN @@ROWCOUNT > 0 THEN 1 ELSE 0 END;
END
