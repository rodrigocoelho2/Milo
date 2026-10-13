/*
    WeightSave
    Records the weigh-in of a date, replacing one already there.
*/
CREATE PROCEDURE [dbo].[WeightSave]
    @UserID    BIGINT,
    @EntryDate DATE,
    @WeightKg  DECIMAL (5, 1)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;

    BEGIN TRANSACTION;

    UPDATE [dbo].[WeightEntry] WITH (UPDLOCK, HOLDLOCK)
    SET [WeightKg] = @WeightKg,
        [UpdatedAt] = SYSUTCDATETIME()
    WHERE [UserID] = @UserID
      AND [EntryDate] = @EntryDate;

    IF @@ROWCOUNT = 0
    BEGIN
        INSERT INTO [dbo].[WeightEntry] ([UserID], [EntryDate], [WeightKg])
        VALUES (@UserID, @EntryDate, @WeightKg);
    END

    COMMIT TRANSACTION;
END
