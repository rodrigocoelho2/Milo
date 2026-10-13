/*
    DayDeleteRange
    Forgets the logged days from @From to @To (Reset week): ticks, loads, sets and training marks.
*/
CREATE PROCEDURE [dbo].[DayDeleteRange]
    @UserID BIGINT,
    @From   DATE,
    @To     DATE
AS
BEGIN
    SET NOCOUNT ON;

    DELETE FROM [dbo].[Day]
    WHERE [UserID] = @UserID
      AND [DayDate] BETWEEN @From AND @To;
END
