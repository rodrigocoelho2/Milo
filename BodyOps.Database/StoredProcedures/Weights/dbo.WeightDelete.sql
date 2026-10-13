/*
    WeightDelete
    Removes the weigh-in of a date.
*/
CREATE PROCEDURE [dbo].[WeightDelete]
    @UserID    BIGINT,
    @EntryDate DATE,
    @Deleted   BIT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DELETE FROM [dbo].[WeightEntry]
    WHERE [UserID] = @UserID
      AND [EntryDate] = @EntryDate;

    SET @Deleted = CASE WHEN @@ROWCOUNT > 0 THEN 1 ELSE 0 END;
END
