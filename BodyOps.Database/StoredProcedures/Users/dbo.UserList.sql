/*
    UserList
    Every account for Settings › Users, with how much each one has planned and logged.
*/
CREATE PROCEDURE [dbo].[UserList]
AS
BEGIN
    SET NOCOUNT ON;
    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;

    SELECT u.[UserID],
           u.[UserUniqueID],
           u.[Username],
           u.[IsAdmin],
           u.[CreatedAt],
           u.[LastLoginAt],
           (SELECT COUNT(*) FROM [dbo].[PlanExercise] pe WHERE pe.[UserID] = u.[UserID]) AS [PlannedExercises],
           (SELECT COUNT(*) FROM [dbo].[PlanMeal] pm WHERE pm.[UserID] = u.[UserID]) AS [PlannedMeals],
           (SELECT MAX(d.[DayDate]) FROM [dbo].[Day] d WHERE d.[UserID] = u.[UserID]) AS [LastLoggedDay]
    FROM [dbo].[User] u
    ORDER BY u.[UserID];
END
