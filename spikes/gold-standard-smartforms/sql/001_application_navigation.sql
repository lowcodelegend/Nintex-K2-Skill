IF SCHEMA_ID(N'GUX') IS NULL
    EXEC(N'CREATE SCHEMA GUX AUTHORIZATION dbo');
GO

IF OBJECT_ID(N'GUX.NavigationItem', N'U') IS NULL
BEGIN
    CREATE TABLE GUX.NavigationItem
    (
        NavigationCode nvarchar(50) NOT NULL,
        SectionLabel nvarchar(100) NULL,
        Label nvarchar(100) NOT NULL,
        IconToken nvarchar(30) NULL,
        TargetFormName nvarchar(200) NOT NULL,
        SortOrder int NOT NULL,
        IsActive bit NOT NULL CONSTRAINT DF_GUX_NavigationItem_IsActive DEFAULT (1),
        ConfigurationVersion nvarchar(40) NOT NULL CONSTRAINT DF_GUX_NavigationItem_ConfigurationVersion DEFAULT (N'1'),
        CONSTRAINT PK_GUX_NavigationItem PRIMARY KEY CLUSTERED (NavigationCode),
        CONSTRAINT CK_GUX_NavigationItem_SortOrder CHECK (SortOrder >= 0),
        CONSTRAINT CK_GUX_NavigationItem_Label_NotBlank CHECK (LEN(LTRIM(RTRIM(Label))) > 0),
        CONSTRAINT CK_GUX_NavigationItem_Target_NotBlank CHECK (LEN(LTRIM(RTRIM(TargetFormName))) > 0)
    );
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'GUX.NavigationItem')
      AND name = N'UX_GUX_NavigationItem_SortOrder'
)
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX UX_GUX_NavigationItem_SortOrder
        ON GUX.NavigationItem (SortOrder, NavigationCode);
END;
GO

MERGE GUX.NavigationItem AS target
USING
(
    VALUES
        (N'COMMAND', N'Workspace', N'Command centre', N'home', N'GUX.Gold Command Centre', 10, CONVERT(bit, 1), N'1'),
        (N'MY_WORK', N'Workspace', N'My work', N'work', N'GUX.My Work', 20, CONVERT(bit, 1), N'1')
) AS source
(
    NavigationCode,
    SectionLabel,
    Label,
    IconToken,
    TargetFormName,
    SortOrder,
    IsActive,
    ConfigurationVersion
)
ON target.NavigationCode = source.NavigationCode
WHEN MATCHED THEN
    UPDATE SET
        SectionLabel = source.SectionLabel,
        Label = source.Label,
        IconToken = source.IconToken,
        TargetFormName = source.TargetFormName,
        SortOrder = source.SortOrder,
        IsActive = source.IsActive,
        ConfigurationVersion = source.ConfigurationVersion
WHEN NOT MATCHED BY TARGET THEN
    INSERT
    (
        NavigationCode,
        SectionLabel,
        Label,
        IconToken,
        TargetFormName,
        SortOrder,
        IsActive,
        ConfigurationVersion
    )
    VALUES
    (
        source.NavigationCode,
        source.SectionLabel,
        source.Label,
        source.IconToken,
        source.TargetFormName,
        source.SortOrder,
        source.IsActive,
        source.ConfigurationVersion
    );
GO

CREATE OR ALTER VIEW GUX.ApplicationNavigation
AS
    SELECT
        NavigationCode,
        SectionLabel,
        Label,
        IconToken,
        TargetFormName,
        SortOrder,
        IsActive,
        ConfigurationVersion
    FROM GUX.NavigationItem
    WHERE IsActive = CONVERT(bit, 1);
GO
