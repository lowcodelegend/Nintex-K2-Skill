SET NOCOUNT ON;

IF OBJECT_ID(N'CWF.RequestPriority', N'U') IS NULL
BEGIN
    CREATE TABLE CWF.RequestPriority
    (
        PriorityCode nvarchar(20) NOT NULL CONSTRAINT PK_RequestPriority PRIMARY KEY,
        PriorityName nvarchar(100) NOT NULL,
        SortOrder int NOT NULL,
        IsActive bit NOT NULL CONSTRAINT DF_RequestPriority_IsActive DEFAULT (1)
    );
END;
GO

MERGE CWF.RequestPriority AS target
USING (VALUES
    (N'Low', N'Low', 10), (N'Normal', N'Normal', 20),
    (N'High', N'High', 30), (N'Critical', N'Critical', 40)
) AS source (PriorityCode, PriorityName, SortOrder)
ON target.PriorityCode = source.PriorityCode
WHEN MATCHED THEN UPDATE SET PriorityName = source.PriorityName, SortOrder = source.SortOrder
WHEN NOT MATCHED THEN INSERT (PriorityCode, PriorityName, SortOrder) VALUES (source.PriorityCode, source.PriorityName, source.SortOrder);
GO

IF OBJECT_ID(N'CWF.RequestStatus', N'U') IS NULL
BEGIN
    CREATE TABLE CWF.RequestStatus
    (
        StatusCode nvarchar(30) NOT NULL CONSTRAINT PK_RequestStatus PRIMARY KEY,
        StatusName nvarchar(100) NOT NULL,
        SortOrder int NOT NULL
    );
END;
GO

MERGE CWF.RequestStatus AS target
USING (VALUES
    (N'Draft', N'Draft', 10), (N'Submitted', N'Submitted', 20),
    (N'In Review', N'In Review', 30), (N'Approved', N'Approved', 40),
    (N'Rejected', N'Rejected', 50), (N'Cancelled', N'Cancelled', 60)
) AS source (StatusCode, StatusName, SortOrder)
ON target.StatusCode = source.StatusCode
WHEN MATCHED THEN UPDATE SET StatusName = source.StatusName, SortOrder = source.SortOrder
WHEN NOT MATCHED THEN INSERT (StatusCode, StatusName, SortOrder) VALUES (source.StatusCode, source.StatusName, source.SortOrder);
GO

IF OBJECT_ID(N'CWF.ApprovalTaskStatus', N'U') IS NULL
BEGIN
    CREATE TABLE CWF.ApprovalTaskStatus
    (
        StatusCode nvarchar(20) NOT NULL CONSTRAINT PK_ApprovalTaskStatus PRIMARY KEY,
        StatusName nvarchar(100) NOT NULL,
        SortOrder int NOT NULL
    );
END;
GO

MERGE CWF.ApprovalTaskStatus AS target
USING (VALUES (N'Pending', N'Pending', 10), (N'Completed', N'Completed', 20), (N'Cancelled', N'Cancelled', 30)) AS source (StatusCode, StatusName, SortOrder)
ON target.StatusCode = source.StatusCode
WHEN MATCHED THEN UPDATE SET StatusName = source.StatusName, SortOrder = source.SortOrder
WHEN NOT MATCHED THEN INSERT (StatusCode, StatusName, SortOrder) VALUES (source.StatusCode, source.StatusName, source.SortOrder);
GO

IF OBJECT_ID(N'CWF.ApprovalDecision', N'U') IS NULL
BEGIN
    CREATE TABLE CWF.ApprovalDecision
    (
        DecisionCode nvarchar(20) NOT NULL CONSTRAINT PK_ApprovalDecision PRIMARY KEY,
        DecisionName nvarchar(100) NOT NULL,
        SortOrder int NOT NULL
    );
END;
GO

MERGE CWF.ApprovalDecision AS target
USING (VALUES (N'Approved', N'Approved', 10), (N'Rejected', N'Rejected', 20)) AS source (DecisionCode, DecisionName, SortOrder)
ON target.DecisionCode = source.DecisionCode
WHEN MATCHED THEN UPDATE SET DecisionName = source.DecisionName, SortOrder = source.SortOrder
WHEN NOT MATCHED THEN INSERT (DecisionCode, DecisionName, SortOrder) VALUES (source.DecisionCode, source.DecisionName, source.SortOrder);
GO

IF OBJECT_ID(N'CWF.RequestType', N'U') IS NULL
BEGIN
    CREATE TABLE CWF.RequestType
    (
        RequestTypeId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_RequestType PRIMARY KEY,
        RequestTypeCode nvarchar(30) NOT NULL CONSTRAINT UQ_RequestType_RequestTypeCode UNIQUE,
        RequestTypeName nvarchar(100) NOT NULL,
        Description nvarchar(500) NULL,
        DefaultSlaHours int NOT NULL CONSTRAINT DF_RequestType_DefaultSlaHours DEFAULT (48),
        RequiresAmount bit NOT NULL CONSTRAINT DF_RequestType_RequiresAmount DEFAULT (0),
        IsActive bit NOT NULL CONSTRAINT DF_RequestType_IsActive DEFAULT (1)
    );
END;
GO

IF OBJECT_ID(N'CWF.WorkflowRequest', N'U') IS NULL
BEGIN
    CREATE TABLE CWF.WorkflowRequest
    (
        RequestId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_WorkflowRequest PRIMARY KEY,
        RequestNumber nvarchar(30) NOT NULL CONSTRAINT UQ_WorkflowRequest_RequestNumber UNIQUE,
        RequestTypeId int NOT NULL,
        RequestorEmployeeId int NOT NULL,
        DepartmentId int NOT NULL,
        Title nvarchar(200) NOT NULL,
        Description nvarchar(2000) NULL,
        Amount decimal(18,2) NULL,
        Priority nvarchar(20) NOT NULL CONSTRAINT DF_WorkflowRequest_Priority DEFAULT (N'Normal'),
        Status nvarchar(30) NOT NULL CONSTRAINT DF_WorkflowRequest_Status DEFAULT (N'Draft'),
        SubmittedOn datetime2(0) NULL,
        CompletedOn datetime2(0) NULL,
        CreatedOn datetime2(0) NULL CONSTRAINT DF_WorkflowRequest_CreatedOn DEFAULT (SYSUTCDATETIME()),
        ModifiedOn datetime2(0) NULL CONSTRAINT DF_WorkflowRequest_ModifiedOn DEFAULT (SYSUTCDATETIME()),
        RowVersion rowversion NOT NULL,
        CONSTRAINT FK_WorkflowRequest_RequestType FOREIGN KEY (RequestTypeId) REFERENCES CWF.RequestType(RequestTypeId),
        CONSTRAINT FK_WorkflowRequest_Requestor FOREIGN KEY (RequestorEmployeeId) REFERENCES CWF.Employee(EmployeeId),
        CONSTRAINT FK_WorkflowRequest_Department FOREIGN KEY (DepartmentId) REFERENCES CWF.Department(DepartmentId),
        CONSTRAINT FK_WorkflowRequest_Priority FOREIGN KEY (Priority) REFERENCES CWF.RequestPriority(PriorityCode),
        CONSTRAINT FK_WorkflowRequest_Status FOREIGN KEY (Status) REFERENCES CWF.RequestStatus(StatusCode)
    );
END;
GO

IF OBJECT_ID(N'CWF.CK_WorkflowRequest_Priority', N'C') IS NOT NULL
    ALTER TABLE CWF.WorkflowRequest DROP CONSTRAINT CK_WorkflowRequest_Priority;
IF OBJECT_ID(N'CWF.CK_WorkflowRequest_Status', N'C') IS NOT NULL
    ALTER TABLE CWF.WorkflowRequest DROP CONSTRAINT CK_WorkflowRequest_Status;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'CWF.WorkflowRequest') AND name = N'FK_WorkflowRequest_Priority')
    ALTER TABLE CWF.WorkflowRequest WITH CHECK ADD CONSTRAINT FK_WorkflowRequest_Priority FOREIGN KEY (Priority) REFERENCES CWF.RequestPriority(PriorityCode);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'CWF.WorkflowRequest') AND name = N'FK_WorkflowRequest_Status')
    ALTER TABLE CWF.WorkflowRequest WITH CHECK ADD CONSTRAINT FK_WorkflowRequest_Status FOREIGN KEY (Status) REFERENCES CWF.RequestStatus(StatusCode);
GO

IF EXISTS
(
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'CWF.WorkflowRequest')
      AND name = N'CreatedOn'
      AND is_nullable = 0
)
    ALTER TABLE CWF.WorkflowRequest ALTER COLUMN CreatedOn datetime2(0) NULL;
GO

IF EXISTS
(
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'CWF.WorkflowRequest')
      AND name = N'ModifiedOn'
      AND is_nullable = 0
)
    ALTER TABLE CWF.WorkflowRequest ALTER COLUMN ModifiedOn datetime2(0) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'CWF.WorkflowRequest') AND name = N'IX_WorkflowRequest_Status_Requestor')
    CREATE INDEX IX_WorkflowRequest_Status_Requestor ON CWF.WorkflowRequest(Status, RequestorEmployeeId);
GO

IF OBJECT_ID(N'CWF.ApprovalTask', N'U') IS NULL
BEGIN
    CREATE TABLE CWF.ApprovalTask
    (
        ApprovalTaskId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_ApprovalTask PRIMARY KEY,
        RequestId int NOT NULL,
        StepNumber int NOT NULL,
        StepName nvarchar(100) NOT NULL,
        AssignedEmployeeId int NOT NULL,
        Status nvarchar(20) NOT NULL CONSTRAINT DF_ApprovalTask_Status DEFAULT (N'Pending'),
        Decision nvarchar(20) NULL,
        DecisionComment nvarchar(1000) NULL,
        DueOn datetime2(0) NULL,
        ActedOn datetime2(0) NULL,
        CreatedOn datetime2(0) NULL CONSTRAINT DF_ApprovalTask_CreatedOn DEFAULT (SYSUTCDATETIME()),
        RowVersion rowversion NOT NULL,
        CONSTRAINT UQ_ApprovalTask_Request_Step UNIQUE (RequestId, StepNumber),
        CONSTRAINT FK_ApprovalTask_Request FOREIGN KEY (RequestId) REFERENCES CWF.WorkflowRequest(RequestId),
        CONSTRAINT FK_ApprovalTask_AssignedEmployee FOREIGN KEY (AssignedEmployeeId) REFERENCES CWF.Employee(EmployeeId),
        CONSTRAINT FK_ApprovalTask_Status FOREIGN KEY (Status) REFERENCES CWF.ApprovalTaskStatus(StatusCode),
        CONSTRAINT FK_ApprovalTask_Decision FOREIGN KEY (Decision) REFERENCES CWF.ApprovalDecision(DecisionCode)
    );
END;
GO

IF OBJECT_ID(N'CWF.CK_ApprovalTask_Status', N'C') IS NOT NULL
    ALTER TABLE CWF.ApprovalTask DROP CONSTRAINT CK_ApprovalTask_Status;
IF OBJECT_ID(N'CWF.CK_ApprovalTask_Decision', N'C') IS NOT NULL
    ALTER TABLE CWF.ApprovalTask DROP CONSTRAINT CK_ApprovalTask_Decision;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'CWF.ApprovalTask') AND name = N'FK_ApprovalTask_Status')
    ALTER TABLE CWF.ApprovalTask WITH CHECK ADD CONSTRAINT FK_ApprovalTask_Status FOREIGN KEY (Status) REFERENCES CWF.ApprovalTaskStatus(StatusCode);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'CWF.ApprovalTask') AND name = N'FK_ApprovalTask_Decision')
    ALTER TABLE CWF.ApprovalTask WITH CHECK ADD CONSTRAINT FK_ApprovalTask_Decision FOREIGN KEY (Decision) REFERENCES CWF.ApprovalDecision(DecisionCode);
GO

IF EXISTS
(
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'CWF.ApprovalTask')
      AND name = N'CreatedOn'
      AND is_nullable = 0
)
    ALTER TABLE CWF.ApprovalTask ALTER COLUMN CreatedOn datetime2(0) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'CWF.ApprovalTask') AND name = N'IX_ApprovalTask_Assignee_Status')
    CREATE INDEX IX_ApprovalTask_Assignee_Status ON CWF.ApprovalTask(AssignedEmployeeId, Status, DueOn);
GO

IF OBJECT_ID(N'CWF.RequestComment', N'U') IS NULL
BEGIN
    CREATE TABLE CWF.RequestComment
    (
        RequestCommentId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_RequestComment PRIMARY KEY,
        RequestId int NOT NULL,
        AuthorEmployeeId int NOT NULL,
        CommentText nvarchar(2000) NOT NULL,
        IsPrivate bit NOT NULL CONSTRAINT DF_RequestComment_IsPrivate DEFAULT (0),
        CreatedOn datetime2(0) NOT NULL CONSTRAINT DF_RequestComment_CreatedOn DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_RequestComment_Request FOREIGN KEY (RequestId) REFERENCES CWF.WorkflowRequest(RequestId),
        CONSTRAINT FK_RequestComment_Author FOREIGN KEY (AuthorEmployeeId) REFERENCES CWF.Employee(EmployeeId)
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'CWF.RequestComment') AND name = N'IX_RequestComment_RequestId')
    CREATE INDEX IX_RequestComment_RequestId ON CWF.RequestComment(RequestId, CreatedOn);
GO

MERGE CWF.RequestType AS target
USING
(
    VALUES
        (N'PURCHASE', N'Purchase request', N'Approval for purchasing goods or services', 48, 1),
        (N'ACCESS', N'System access request', N'Approval for application or infrastructure access', 24, 0),
        (N'LEAVE', N'Leave request', N'Employee leave approval', 24, 0)
) AS source (RequestTypeCode, RequestTypeName, Description, DefaultSlaHours, RequiresAmount)
ON target.RequestTypeCode = source.RequestTypeCode
WHEN MATCHED THEN
    UPDATE SET RequestTypeName = source.RequestTypeName,
               Description = source.Description,
               DefaultSlaHours = source.DefaultSlaHours,
               RequiresAmount = source.RequiresAmount,
               IsActive = 1
WHEN NOT MATCHED THEN
    INSERT (RequestTypeCode, RequestTypeName, Description, DefaultSlaHours, RequiresAmount)
    VALUES (source.RequestTypeCode, source.RequestTypeName, source.Description, source.DefaultSlaHours, source.RequiresAmount);
GO
