/*
    UserCount
    Number of accounts (0 means the next account is the first one and becomes the admin).
*/
CREATE PROCEDURE [dbo].[UserCount]
AS
BEGIN
    SET NOCOUNT ON;
    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;

    SELECT COUNT(*) AS [UserCount]
    FROM [dbo].[User];
END
