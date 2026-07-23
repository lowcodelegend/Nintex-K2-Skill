IF SCHEMA_ID(N'SPL') IS NULL
    EXEC(N'CREATE SCHEMA SPL AUTHORIZATION dbo');
GO

IF OBJECT_ID(N'SPL.NavigationItem', N'U') IS NULL
BEGIN
    CREATE TABLE SPL.NavigationItem
    (
        NavigationCode nvarchar(50) NOT NULL,
        SectionLabel nvarchar(100) NULL,
        Label nvarchar(100) NOT NULL,
        IconToken nvarchar(30) NULL,
        TargetFormName nvarchar(200) NOT NULL,
        SortOrder int NOT NULL,
        IsActive bit NOT NULL CONSTRAINT DF_SPL_NavigationItem_IsActive DEFAULT (1),
        ConfigurationVersion nvarchar(40) NOT NULL
            CONSTRAINT DF_SPL_NavigationItem_ConfigurationVersion DEFAULT (N'1'),
        CONSTRAINT PK_SPL_NavigationItem PRIMARY KEY CLUSTERED (NavigationCode),
        CONSTRAINT CK_SPL_NavigationItem_SortOrder CHECK (SortOrder >= 0)
    );
END;
GO

IF OBJECT_ID(N'SPL.WorkItem', N'U') IS NULL
BEGIN
    CREATE TABLE SPL.WorkItem
    (
        WorkItemId int IDENTITY(1,1) NOT NULL,
        Reference nvarchar(30) NOT NULL,
        Title nvarchar(160) NOT NULL,
        OwnerName nvarchar(100) NOT NULL,
        Status nvarchar(30) NOT NULL,
        Priority nvarchar(20) NOT NULL,
        DueDate date NOT NULL,
        UpdatedDate datetime2(0) NOT NULL CONSTRAINT DF_SPL_WorkItem_UpdatedDate DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_SPL_WorkItem PRIMARY KEY CLUSTERED (WorkItemId),
        CONSTRAINT UQ_SPL_WorkItem_Reference UNIQUE (Reference)
    );
END;
GO

MERGE SPL.NavigationItem AS target
USING
(
    VALUES
        (N'DASHBOARD', N'Workspace', N'Overview', N'home', N'SPL.Link Dashboard', 10, 1, N'1'),
        (N'WORK', N'Workspace', N'Work items', N'work', N'SPL.Link Work', 20, 1, N'1')
) AS source
(
    NavigationCode, SectionLabel, Label, IconToken, TargetFormName,
    SortOrder, IsActive, ConfigurationVersion
)
ON target.NavigationCode = source.NavigationCode
WHEN MATCHED THEN UPDATE SET
    SectionLabel = source.SectionLabel,
    Label = source.Label,
    IconToken = source.IconToken,
    TargetFormName = source.TargetFormName,
    SortOrder = source.SortOrder,
    IsActive = source.IsActive,
    ConfigurationVersion = source.ConfigurationVersion
WHEN NOT MATCHED THEN INSERT
(
    NavigationCode, SectionLabel, Label, IconToken, TargetFormName,
    SortOrder, IsActive, ConfigurationVersion
)
VALUES
(
    source.NavigationCode, source.SectionLabel, source.Label, source.IconToken,
    source.TargetFormName, source.SortOrder, source.IsActive, source.ConfigurationVersion
);
GO

MERGE SPL.WorkItem AS target
USING
(
    VALUES
        (N'WI-1042', N'Review supplier onboarding pack', N'Jordan Lee', N'In progress', N'High', DATEADD(day, 1, CONVERT(date, SYSUTCDATETIME()))),
        (N'WI-1043', N'Confirm insurance documentation', N'Morgan Diaz', N'Open', N'Medium', DATEADD(day, 3, CONVERT(date, SYSUTCDATETIME()))),
        (N'WI-1044', N'Approve updated quality plan', N'Alex Chen', N'Open', N'High', DATEADD(day, 5, CONVERT(date, SYSUTCDATETIME()))),
        (N'WI-1045', N'Archive completed assessment', N'Taylor Reed', N'Completed', N'Low', DATEADD(day, -2, CONVERT(date, SYSUTCDATETIME()))),
        (N'WI-1046', N'Schedule quarterly review', N'Jordan Lee', N'Open', N'Medium', DATEADD(day, 8, CONVERT(date, SYSUTCDATETIME())))
) AS source (Reference, Title, OwnerName, Status, Priority, DueDate)
ON target.Reference = source.Reference
WHEN MATCHED THEN UPDATE SET
    Title = source.Title,
    OwnerName = source.OwnerName,
    Status = source.Status,
    Priority = source.Priority,
    DueDate = source.DueDate
WHEN NOT MATCHED THEN INSERT (Reference, Title, OwnerName, Status, Priority, DueDate)
VALUES (source.Reference, source.Title, source.OwnerName, source.Status, source.Priority, source.DueDate);
GO

CREATE OR ALTER VIEW SPL.ApplicationNavigation
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
    FROM SPL.NavigationItem
    WHERE IsActive = CONVERT(bit, 1);
GO

CREATE OR ALTER VIEW SPL.DashboardSummary
AS
    SELECT
        COUNT_BIG(*) AS TotalItems,
        CONVERT(bigint, SUM(CASE WHEN Status <> N'Completed' THEN 1 ELSE 0 END)) AS OpenItems,
        CONVERT(bigint, SUM(CASE WHEN Priority = N'High' AND Status <> N'Completed' THEN 1 ELSE 0 END)) AS HighPriorityItems,
        CONVERT(bigint, SUM(CASE WHEN Status = N'Completed' THEN 1 ELSE 0 END)) AS CompletedItems
    FROM SPL.WorkItem;
GO
