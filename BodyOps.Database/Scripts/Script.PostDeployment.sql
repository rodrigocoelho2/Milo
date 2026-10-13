/*
    Post-deployment script of BodyOps.Database.
    Runs after every publish of the dacpac. There is no reference data to seed: accounts are created from the app
    (the first one becomes the admin) and each account builds or imports its own plan.
*/
PRINT N'BodyOps.Database deployed.';
