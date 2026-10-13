/*
    WeightImport
    Adds weigh-ins from a backup: @Json is [{"date":"2026-07-14","weightKg":100.4}, …]. Existing dates are replaced.
*/
CREATE PROCEDURE [dbo].[WeightImport]
    @UserID BIGINT,
    @Json   NVARCHAR (MAX)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    MERGE [dbo].[WeightEntry] AS target
    USING (
        SELECT j.[EntryDate], j.[WeightKg]
        FROM OPENJSON(@Json) WITH ([EntryDate] DATE '$.date', [WeightKg] DECIMAL (5, 1) '$.weightKg') j
    ) AS source
    ON target.[UserID] = @UserID AND target.[EntryDate] = source.[EntryDate]
    WHEN MATCHED THEN
        UPDATE SET [WeightKg] = source.[WeightKg], [UpdatedAt] = SYSUTCDATETIME()
    WHEN NOT MATCHED THEN
        INSERT ([UserID], [EntryDate], [WeightKg]) VALUES (@UserID, source.[EntryDate], source.[WeightKg]);

    COMMIT TRANSACTION;
END
