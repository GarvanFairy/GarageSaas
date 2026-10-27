-- GUARDED UNBIND. Separate approval required; TestSubject must equal the verified bound claim.
-- No sign-in token, password, email matching or platform-admin elevation is used.
-- Caller MUST validate TestSubject as exactly canonical 36-character ASCII GUID
-- text before SQLCMD substitution. Never interpolate an arbitrary claim string.
SET XACT_ABORT ON;
IF @@TRANCOUNT <> 0 THROW 51000, 'Run without an ambient transaction.', 1;
IF DB_NAME() <> N'GarageSaas_Staging' THROW 51000, 'Wrong database.', 1;
IF CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')) NOT IN (N'garagesaasdbserver',N'garagesaasdbserver.database.windows.net') THROW 51000, 'Wrong server.', 1;
DECLARE @subject nvarchar(max)=N'$(TestSubject)';
IF DATALENGTH(@subject) <> 72 OR TRY_CONVERT(uniqueidentifier,@subject) IS NULL
    OR SUBSTRING(@subject,9,1) <> N'-' OR SUBSTRING(@subject,14,1) <> N'-'
    OR SUBSTRING(@subject,19,1) <> N'-' OR SUBSTRING(@subject,24,1) <> N'-'
    OR LEN(REPLACE(@subject,N'-',N'')) <> 32
    OR REPLACE(@subject,N'-',N'') COLLATE Latin1_General_100_BIN2 LIKE N'%[^0-9A-Fa-f]%'
    THROW 51000, 'Supply the exact verified canonical GUID subject; review non-GUID subjects separately.', 1;
-- PREPARATION ONLY: execute only after separate unbind approval.
-- Removes access on subsequent requests; does not revoke B2C login or undo workflow writes.
BEGIN TRY
    BEGIN TRANSACTION;
    IF NOT EXISTS(SELECT 1 FROM dbo.Users WITH (UPDLOCK,HOLDLOCK)
        WHERE Id=1 AND CreatedBy=N'staging-fixture' AND ExternalUserId COLLATE Latin1_General_100_BIN2=@subject COLLATE Latin1_General_100_BIN2
          AND IsPlatformAdministrator=0 AND Admin_Owner=0 AND Active=1 AND Blocked=0 AND GarageBusinessId=1)
        THROW 51000, 'Expected exact active non-admin synthetic binding required.', 1;
    IF (SELECT COUNT(*) FROM dbo.Users WITH (UPDLOCK,HOLDLOCK) WHERE ExternalUserId=@subject) <> 1
        THROW 51000, 'Expected exactly one existing subject mapping.', 1;
    IF (SELECT COUNT(*) FROM dbo.GarageBusinessUser WITH (UPDLOCK,HOLDLOCK) WHERE UserId=1) <> 2
        THROW 51000, 'Expected exactly two tester memberships.', 1;
    IF (SELECT COUNT(*) FROM dbo.GarageBusinessUser WITH (UPDLOCK,HOLDLOCK)
        WHERE UserId=1 AND ((Id=1 AND GarageBusinessId=1) OR (Id=2 AND GarageBusinessId=2))
          AND CreatedBy=N'staging-fixture' AND IsActive=1 AND IsOwner=1 AND Role=N'Owner') <> 2
        THROW 51000, 'Synthetic tester memberships differ from approved fixture.', 1;
    IF (SELECT COUNT(*) FROM dbo.GarageBusiness WITH (UPDLOCK,HOLDLOCK)
        WHERE ((Id=1 AND GarageBusinessName=N'SYNTHETIC Staging Garage 1')
            OR (Id=2 AND GarageBusinessName=N'SYNTHETIC Staging Garage 2'))
          AND CreatedBy=N'staging-fixture' AND Active=1 AND Blocked=0) <> 2
        THROW 51000, 'Expected active, unblocked synthetic parent garages.', 1;

    UPDATE dbo.Users SET ExternalUserId=NULL
        WHERE Id=1 AND CreatedBy=N'staging-fixture' AND ExternalUserId COLLATE Latin1_General_100_BIN2=@subject COLLATE Latin1_General_100_BIN2
          AND IsPlatformAdministrator=0 AND Admin_Owner=0 AND Active=1 AND Blocked=0 AND GarageBusinessId=1;
    IF @@ROWCOUNT <> 1 THROW 51000, 'Expected exactly one unbind update.', 1;
    -- No membership or privilege columns are written by this script.
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

