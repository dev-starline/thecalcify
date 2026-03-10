-- 00_add_table_schema_version.sql
IF OBJECT_ID('dbo.SchemaVersions', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.SchemaVersions (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        ScriptName NVARCHAR(255) NOT NULL,
        AppliedOn DATETIME NOT NULL DEFAULT GETDATE(),
        AppliedBy NVARCHAR(100) NULL,
        Checksum NVARCHAR(64) NULL
    );

    ALTER TABLE dbo.SchemaVersions
    ADD CONSTRAINT UQ_SchemaVersions_ScriptName UNIQUE (ScriptName);
END
GO
