/* PREPARED ONLY. Never executed as part of package preparation.
   Run this complete batch in a fresh connection to lpc:GARVAN_ASUS / Garagesaas.
   Defaults are PREVIEW ONLY. Do not run highlighted fragments.
   Retain one freshly generated DeploymentId for this deployment and its reruns.
   No USE, EF migration, business-row DML, or DROP_EXISTING is used. */
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Execute bit = 0;                       -- deliberate execution approval
DECLARE @MaintenanceWindowConfirmed bit = 0;    -- writes/long readers drained
DECLARE @DeploymentId uniqueidentifier = NULL; -- record outside the DB; reuse on rerun
DECLARE @Operation varchar(10) = 'DEPLOY';
DECLARE @Package nvarchar(128) = N'GarageSaas.ThreeAssociationIndexes.v1';
DECLARE @Resource nvarchar(255) = N'GarageSaas.ThreeAssociationIndexes.v1';
DECLARE @Message nvarchar(2048);

IF @@TRANCOUNT <> 0 OR (2 & @@OPTIONS) = 2
    THROW 51000, 'Use a fresh session: no ambient or implicit transaction is permitted.', 1;
IF DB_NAME() COLLATE Latin1_General_100_BIN2 <> N'Garagesaas' COLLATE Latin1_General_100_BIN2
    THROW 51000, 'Wrong database. Connect explicitly to Garagesaas; this script never switches catalogs.', 1;
IF UPPER(ISNULL(CONVERT(nvarchar(128), SERVERPROPERTY('MachineName')),N'')) <> N'GARVAN_ASUS'
 OR UPPER(ISNULL(CONVERT(nvarchar(128), SERVERPROPERTY('ComputerNamePhysicalNetBIOS')),N'')) <> N'GARVAN_ASUS'
 OR UPPER(ISNULL(CONVERT(nvarchar(128), SERVERPROPERTY('ServerName')),N'')) <> N'GARVAN_ASUS'
 OR UPPER(ISNULL(CONVERT(nvarchar(128), @@SERVERNAME),N'')) <> N'GARVAN_ASUS'
 OR SERVERPROPERTY('InstanceName') IS NOT NULL
 OR ISNULL(CONVERT(int, SERVERPROPERTY('IsClustered')), 1) <> 0
 OR ISNULL(CONVERT(int, SERVERPROPERTY('IsLocalDB')), 1) <> 0
 OR ISNULL(CONVERT(nvarchar(128), CONNECTIONPROPERTY('net_transport')), N'') <> N'Shared memory'
    THROW 51000, 'Resolved server/physical host/default instance/shared-memory identity is not approved.', 1;
IF ISNULL(CONVERT(int, SERVERPROPERTY('ProductMajorVersion')), 0) <> 17
 OR ISNULL(CONVERT(int, SERVERPROPERTY('EngineEdition')), 0) <> 2
    THROW 51000, 'Package reviewed for SQL Server 17.x Standard/Standard Developer engine only. Re-review a changed target.', 1;
IF NOT EXISTS (SELECT 1 FROM sys.databases WHERE database_id = DB_ID()
    AND database_id > 4 AND state_desc = 'ONLINE' AND is_read_only = 0 AND source_database_id IS NULL)
    THROW 51000, 'Target must be the online writable ordinary application database, not a snapshot/system database.', 1;
IF ISNULL(HAS_PERMS_BY_NAME(DB_NAME(), 'DATABASE', 'VIEW DEFINITION'), 0) <> 1
 OR ISNULL(HAS_PERMS_BY_NAME(NULL, NULL, 'VIEW ANY DEFINITION'), 0) <> 1
    THROW 51000, 'Complete database/server definition visibility is required for fail-closed metadata checks.', 1;
IF EXISTS (SELECT 1 FROM sys.triggers WHERE parent_class = 0 AND is_disabled = 0)
 OR EXISTS (SELECT 1 FROM sys.server_triggers WHERE is_disabled = 0)
 OR EXISTS (SELECT 1 FROM sys.event_notifications)
 OR EXISTS (SELECT 1 FROM sys.server_event_notifications)
    THROW 51000, 'Unreviewed DDL hooks/notifications exist. Stop; do not disable or bypass them.', 1;
IF @Execute = 1 AND (@MaintenanceWindowConfirmed <> 1 OR @DeploymentId IS NULL
    OR @DeploymentId = '00000000-0000-0000-0000-000000000000')
    THROW 51000, 'Execution requires a confirmed maintenance window and a recorded nonzero DeploymentId.', 1;

DECLARE @Spec table
(
    Ord int NOT NULL, TableName sysname NOT NULL, IndexName sysname NOT NULL,
    Key1 sysname NOT NULL, Key2 sysname NULL, IncludeColumn sysname NOT NULL, ObjectId int NULL
);
INSERT @Spec (Ord, TableName, IndexName, Key1, Key2, IncludeColumn)
VALUES
 (1, N'VehicleInvoice', N'IX_VehicleInvoice_WorkQuoteId', N'WorkQuoteId', NULL, N'GarageBusinessId'),
 (2, N'InvoiceWorkQuote', N'IX_InvoiceWorkQuote_WorkQuoteId_InvoiceId', N'WorkQuoteId', N'InvoiceId', N'GarageBusinessCustomerId'),
 (3, N'InvoiceWorkQuote', N'IX_InvoiceWorkQuote_InvoiceId_WorkQuoteId', N'InvoiceId', N'WorkQuoteId', N'GarageBusinessCustomerId');
UPDATE @Spec SET ObjectId = OBJECT_ID(N'dbo.' + QUOTENAME(TableName), N'U');
IF EXISTS (SELECT 1 FROM @Spec s LEFT JOIN sys.tables t ON t.object_id = s.ObjectId
    WHERE t.object_id IS NULL OR t.is_ms_shipped = 1 OR t.is_memory_optimized = 1 OR t.temporal_type <> 0 OR t.is_external = 1)
    THROW 51000, 'Expected ordinary dbo rowstore tables are missing or changed.', 1;

DECLARE @Columns table (TableName sysname, ColumnName sysname, Nullable bit, IdentityColumn bit);
INSERT @Columns VALUES
 (N'VehicleInvoice', N'Id', 0, 1), (N'VehicleInvoice', N'WorkQuoteId', 1, 0), (N'VehicleInvoice', N'GarageBusinessId', 1, 0),
 (N'InvoiceWorkQuote', N'Id', 0, 1), (N'InvoiceWorkQuote', N'WorkQuoteId', 1, 0),
 (N'InvoiceWorkQuote', N'InvoiceId', 1, 0), (N'InvoiceWorkQuote', N'GarageBusinessCustomerId', 1, 0);
IF EXISTS
(
    SELECT 1 FROM @Columns e LEFT JOIN sys.columns c
      ON c.object_id = OBJECT_ID(N'dbo.' + QUOTENAME(e.TableName), N'U') AND c.name = e.ColumnName
    WHERE c.column_id IS NULL OR c.system_type_id <> 56 OR c.user_type_id <> 56
       OR c.max_length <> 4 OR c.is_nullable <> e.Nullable OR c.is_identity <> e.IdentityColumn
       OR c.is_computed = 1 OR c.generated_always_type <> 0 OR c.is_sparse = 1
)
    THROW 51000, 'Required native int column definitions/nullability/identities differ from the reviewed schema.', 1;
IF EXISTS
(
    SELECT 1 FROM @Spec s WHERE NOT EXISTS
    (
        SELECT 1 FROM sys.indexes i
        WHERE i.object_id = s.ObjectId AND i.is_primary_key = 1 AND i.type = 1 AND i.is_disabled = 0
          AND (SELECT COUNT(*) FROM sys.index_columns k WHERE k.object_id = i.object_id AND k.index_id = i.index_id AND k.key_ordinal > 0) = 1
          AND EXISTS (SELECT 1 FROM sys.index_columns k JOIN sys.columns c ON c.object_id=k.object_id AND c.column_id=k.column_id
              WHERE k.object_id=i.object_id AND k.index_id=i.index_id AND k.key_ordinal=1 AND c.name=N'Id' AND k.is_descending_key=0)
    )
)
    THROW 51000, 'Expected enabled clustered primary keys on Id are missing or changed.', 1;
IF EXISTS (SELECT 1 FROM @Spec s JOIN sys.indexes i ON i.object_id=s.ObjectId
    JOIN sys.data_spaces ds ON ds.data_space_id=i.data_space_id WHERE i.index_id=1 AND ds.type <> 'FG')
    THROW 51000, 'Partitioned base tables require a separate deployment design.', 1;
IF @Execute=1 AND EXISTS (SELECT 1 FROM @Spec s WHERE ISNULL(HAS_PERMS_BY_NAME(N'dbo.' + QUOTENAME(s.TableName), 'OBJECT', 'ALTER'), 0) <> 1)
    THROW 51000, 'ALTER permission on both target tables is required.', 1;

DECLARE @Existing table
(
    Ord int, IndexId int, IndexName sysname, Equivalent bit, ExactShape bit,
    FirstKey sysname NULL, OwnerPackage nvarchar(128) NULL, OwnerDeployment uniqueidentifier NULL, DefinitionSignature varbinary(32) NULL
);
/* Equivalent: enabled unfiltered nonunique rowstore NCI; exact ASC key order;
   requested explicit INCLUDE present. Extra INCLUDE columns are accepted ONLY
   as a pre-existing equivalent. Rollback demands the exact one-INCLUDE shape. */
INSERT @Existing (Ord,IndexId,IndexName,Equivalent,ExactShape,FirstKey,OwnerPackage,OwnerDeployment)
SELECT s.Ord, i.index_id, i.name,
 CONVERT(bit, CASE WHEN i.type=2 AND i.is_unique=0 AND i.is_primary_key=0 AND i.is_unique_constraint=0
     AND EXISTS (SELECT 1 FROM sys.data_spaces ds WHERE ds.data_space_id=i.data_space_id AND ds.type='FG')
     AND i.is_disabled=0 AND i.is_hypothetical=0 AND i.has_filter=0
     AND (SELECT COUNT(*) FROM sys.index_columns k WHERE k.object_id=i.object_id AND k.index_id=i.index_id AND k.key_ordinal>0) = CASE WHEN s.Key2 IS NULL THEN 1 ELSE 2 END
     AND EXISTS (SELECT 1 FROM sys.index_columns k JOIN sys.columns c ON c.object_id=k.object_id AND c.column_id=k.column_id
         WHERE k.object_id=i.object_id AND k.index_id=i.index_id AND k.key_ordinal=1 AND k.is_descending_key=0 AND c.name=s.Key1)
     AND (s.Key2 IS NULL OR EXISTS (SELECT 1 FROM sys.index_columns k JOIN sys.columns c ON c.object_id=k.object_id AND c.column_id=k.column_id
         WHERE k.object_id=i.object_id AND k.index_id=i.index_id AND k.key_ordinal=2 AND k.is_descending_key=0 AND c.name=s.Key2))
     AND EXISTS (SELECT 1 FROM sys.index_columns k JOIN sys.columns c ON c.object_id=k.object_id AND c.column_id=k.column_id
         WHERE k.object_id=i.object_id AND k.index_id=i.index_id AND k.is_included_column=1 AND c.name=s.IncludeColumn)
     THEN 1 ELSE 0 END),
 CONVERT(bit, CASE WHEN (SELECT COUNT(*) FROM sys.index_columns k WHERE k.object_id=i.object_id AND k.index_id=i.index_id AND k.is_included_column=1)=1 THEN 1 ELSE 0 END),
 (SELECT c.name FROM sys.index_columns k JOIN sys.columns c ON c.object_id=k.object_id AND c.column_id=k.column_id
     WHERE k.object_id=i.object_id AND k.index_id=i.index_id AND k.key_ordinal=1),
 CONVERT(nvarchar(128), p.value), TRY_CONVERT(uniqueidentifier, CONVERT(nvarchar(128), d.value))
FROM @Spec s JOIN sys.indexes i ON i.object_id=s.ObjectId AND i.index_id>0
LEFT JOIN sys.extended_properties p ON p.class=7 AND p.major_id=i.object_id AND p.minor_id=i.index_id AND p.name=N'GarageSaas.IndexPackage'
LEFT JOIN sys.extended_properties d ON d.class=7 AND d.major_id=i.object_id AND d.minor_id=i.index_id AND d.name=N'GarageSaas.DeploymentId';

-- Fingerprint metadata only; used to reject stale preflight inventories.
UPDATE e SET DefinitionSignature=HASHBYTES('SHA2_256',CONVERT(varbinary(max),CONCAT(i.type,N'|',i.is_unique,N'|',i.is_primary_key,N'|',i.is_unique_constraint,N'|',
 i.is_disabled,N'|',i.is_hypothetical,N'|',i.has_filter,N'|',i.filter_definition,N'|',i.data_space_id,N'|',
 (SELECT CONCAT(QUOTENAME(c.name),N':',k.key_ordinal,N':',k.is_descending_key,N':',k.is_included_column,N';')
  FROM sys.index_columns k JOIN sys.columns c ON c.object_id=k.object_id AND c.column_id=k.column_id
  WHERE k.object_id=i.object_id AND k.index_id=i.index_id ORDER BY k.index_column_id FOR XML PATH(''),TYPE).value('.','nvarchar(max)'))))
FROM @Existing e JOIN @Spec s ON s.Ord=e.Ord
JOIN sys.indexes i ON i.object_id=s.ObjectId AND i.index_id=e.IndexId;
SELECT DB_NAME() AS DatabaseName, ORIGINAL_LOGIN() AS OperatorLogin,
    SERVERPROPERTY('ServerName') AS ResolvedServer, CONNECTIONPROPERTY('net_transport') AS Transport,
    SERVERPROPERTY('ProductVersion') AS ProductVersion, SERVERPROPERTY('Edition') AS Edition,
    N'OFFLINE only; MAXDOP=1; no database/isolation/lock-protocol changes' AS BuildPolicy;
SELECT s.TableName, c.name AS ColumnName, TYPE_NAME(c.user_type_id) AS SqlType,
    c.max_length, c.is_nullable, c.is_identity
FROM (SELECT DISTINCT TableName,ObjectId FROM @Spec) s JOIN sys.columns c ON c.object_id=s.ObjectId
WHERE EXISTS (SELECT 1 FROM @Columns e WHERE e.TableName=s.TableName AND e.ColumnName=c.name)
ORDER BY s.TableName,c.column_id;
SELECT DISTINCT s.TableName,HAS_PERMS_BY_NAME(N'dbo.'+QUOTENAME(s.TableName),'OBJECT','ALTER') AS CanAlterTargetTable FROM @Spec s;
SELECT s.TableName, e.* FROM @Spec s JOIN @Existing e ON e.Ord=s.Ord ORDER BY s.Ord,e.IndexId;

IF EXISTS (SELECT 1 FROM @Spec s JOIN @Existing e ON e.Ord=s.Ord AND e.IndexName=s.IndexName WHERE e.Equivalent=0)
    THROW 51000, 'A candidate name already exists with a different definition. Nothing may be replaced automatically.', 1;
IF EXISTS (SELECT 1 FROM @Spec s JOIN @Existing e ON e.Ord=s.Ord AND e.IndexName=s.IndexName
    WHERE e.OwnerPackage=@Package AND (e.OwnerDeployment IS NULL OR e.ExactShape=0 OR e.Equivalent=0))
    THROW 51000, 'A package-owned candidate has corrupt ownership or a changed definition; manual review required.', 1;
IF @Operation='DEPLOY' AND EXISTS
(
    SELECT 1 FROM @Spec s WHERE NOT EXISTS (SELECT 1 FROM @Existing e WHERE e.Ord=s.Ord AND e.Equivalent=1)
     AND EXISTS (SELECT 1 FROM @Existing e WHERE e.Ord=s.Ord AND e.FirstKey=s.Key1)
)
    THROW 51000, 'An overlapping non-equivalent access path exists. Stop for review rather than add a possibly redundant index.', 1;
IF @Operation='DEPLOY' AND @Execute=1 AND EXISTS
(
    SELECT 1 FROM @Spec s JOIN @Existing e ON e.Ord=s.Ord AND e.IndexName=s.IndexName
    WHERE e.OwnerPackage=@Package AND e.OwnerDeployment<>@DeploymentId
)
    THROW 51000, 'A candidate belongs to a different deployment. Recover its change record; do not adopt or overwrite it.', 1;

SELECT s.Ord,s.TableName,s.IndexName,
 CASE WHEN @Operation='ROLLBACK' THEN
     CASE WHEN EXISTS (SELECT 1 FROM @Existing e WHERE e.Ord=s.Ord AND e.IndexName=s.IndexName
         AND e.OwnerPackage=@Package AND e.OwnerDeployment=@DeploymentId AND e.Equivalent=1 AND e.ExactShape=1)
         THEN N'DROP_OWNED_ONLY' ELSE N'PRESERVE_NOT_OWNED_OR_ABSENT' END
 ELSE CASE
     WHEN EXISTS (SELECT 1 FROM @Existing e WHERE e.Ord=s.Ord AND e.IndexName=s.IndexName AND e.OwnerPackage=@Package AND e.OwnerDeployment=@DeploymentId)
         THEN N'ALREADY_OWNED_SAME_DEPLOYMENT'
     WHEN EXISTS (SELECT 1 FROM @Existing e WHERE e.Ord=s.Ord AND e.Equivalent=1) THEN N'PRESERVE_EXISTING_EQUIVALENT'
     ELSE N'CREATE_AND_MARK' END END AS PlannedAction
FROM @Spec s ORDER BY s.Ord;

-- END SHARED PREFLIGHT. No permanent DDL/DML above this line.
PRINT 'PREFLIGHT ONLY. No deployment or rollback statements are present in this script.';


