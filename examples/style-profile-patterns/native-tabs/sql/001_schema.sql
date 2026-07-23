IF SCHEMA_ID(N'SPT') IS NULL
    EXEC(N'CREATE SCHEMA SPT AUTHORIZATION dbo');
GO

IF OBJECT_ID(N'SPT.WorkItem', N'U') IS NULL
BEGIN
    CREATE TABLE SPT.WorkItem
    (
        WorkItemId int IDENTITY(1,1) NOT NULL,
        Reference nvarchar(30) NOT NULL,
        Title nvarchar(160) NOT NULL,
        Description nvarchar(500) NOT NULL,
        OwnerName nvarchar(100) NOT NULL,
        Status nvarchar(30) NOT NULL,
        Priority nvarchar(20) NOT NULL,
        DueDate date NOT NULL,
        CONSTRAINT PK_SPT_WorkItem PRIMARY KEY CLUSTERED (WorkItemId),
        CONSTRAINT UQ_SPT_WorkItem_Reference UNIQUE (Reference)
    );
END;
GO

MERGE SPT.WorkItem AS target
USING
(
    VALUES
        (N'TAB-201', N'Prepare launch readiness review', N'Consolidate the final operational readiness evidence.', N'Jordan Lee', N'In progress', N'High', DATEADD(day, 1, CONVERT(date, SYSUTCDATETIME()))),
        (N'TAB-202', N'Confirm training completion', N'Validate attendance and outstanding learning actions.', N'Morgan Diaz', N'Open', N'Medium', DATEADD(day, 3, CONVERT(date, SYSUTCDATETIME()))),
        (N'TAB-203', N'Review support handover', N'Approve the support ownership and escalation schedule.', N'Alex Chen', N'Open', N'High', DATEADD(day, 5, CONVERT(date, SYSUTCDATETIME()))),
        (N'TAB-204', N'Close pilot feedback', N'Archive resolved pilot findings and decisions.', N'Taylor Reed', N'Completed', N'Low', DATEADD(day, -1, CONVERT(date, SYSUTCDATETIME())))
) AS source (Reference, Title, Description, OwnerName, Status, Priority, DueDate)
ON target.Reference = source.Reference
WHEN MATCHED THEN UPDATE SET
    Title = source.Title,
    Description = source.Description,
    OwnerName = source.OwnerName,
    Status = source.Status,
    Priority = source.Priority,
    DueDate = source.DueDate
WHEN NOT MATCHED THEN INSERT (Reference, Title, Description, OwnerName, Status, Priority, DueDate)
VALUES (source.Reference, source.Title, source.Description, source.OwnerName, source.Status, source.Priority, source.DueDate);
GO

CREATE OR ALTER VIEW SPT.DashboardSummary
AS
    SELECT
        COUNT_BIG(*) AS TotalItems,
        CONVERT(bigint, SUM(CASE WHEN Status <> N'Completed' THEN 1 ELSE 0 END)) AS OpenItems,
        CONVERT(bigint, SUM(CASE WHEN Priority = N'High' AND Status <> N'Completed' THEN 1 ELSE 0 END)) AS HighPriorityItems,
        CONVERT(bigint, SUM(CASE WHEN Status = N'Completed' THEN 1 ELSE 0 END)) AS CompletedItems
    FROM SPT.WorkItem;
GO
