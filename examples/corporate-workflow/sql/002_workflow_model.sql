SET NOCOUNT ON;

IF OBJECT_ID(N'app.RequestType', N'U') IS NULL
BEGIN
    CREATE TABLE app.RequestType
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

IF OBJECT_ID(N'app.WorkflowRequest', N'U') IS NULL
BEGIN
    CREATE TABLE app.WorkflowRequest
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
        CreatedOn datetime2(0) NOT NULL CONSTRAINT DF_WorkflowRequest_CreatedOn DEFAULT (SYSUTCDATETIME()),
        ModifiedOn datetime2(0) NOT NULL CONSTRAINT DF_WorkflowRequest_ModifiedOn DEFAULT (SYSUTCDATETIME()),
        RowVersion rowversion NOT NULL,
        CONSTRAINT FK_WorkflowRequest_RequestType FOREIGN KEY (RequestTypeId) REFERENCES app.RequestType(RequestTypeId),
        CONSTRAINT FK_WorkflowRequest_Requestor FOREIGN KEY (RequestorEmployeeId) REFERENCES org.Employee(EmployeeId),
        CONSTRAINT FK_WorkflowRequest_Department FOREIGN KEY (DepartmentId) REFERENCES org.Department(DepartmentId),
        CONSTRAINT CK_WorkflowRequest_Priority CHECK (Priority IN (N'Low', N'Normal', N'High', N'Critical')),
        CONSTRAINT CK_WorkflowRequest_Status CHECK (Status IN (N'Draft', N'Submitted', N'In Review', N'Approved', N'Rejected', N'Cancelled'))
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'app.WorkflowRequest') AND name = N'IX_WorkflowRequest_Status_Requestor')
    CREATE INDEX IX_WorkflowRequest_Status_Requestor ON app.WorkflowRequest(Status, RequestorEmployeeId);
GO

IF OBJECT_ID(N'app.ApprovalTask', N'U') IS NULL
BEGIN
    CREATE TABLE app.ApprovalTask
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
        CreatedOn datetime2(0) NOT NULL CONSTRAINT DF_ApprovalTask_CreatedOn DEFAULT (SYSUTCDATETIME()),
        RowVersion rowversion NOT NULL,
        CONSTRAINT UQ_ApprovalTask_Request_Step UNIQUE (RequestId, StepNumber),
        CONSTRAINT FK_ApprovalTask_Request FOREIGN KEY (RequestId) REFERENCES app.WorkflowRequest(RequestId),
        CONSTRAINT FK_ApprovalTask_AssignedEmployee FOREIGN KEY (AssignedEmployeeId) REFERENCES org.Employee(EmployeeId),
        CONSTRAINT CK_ApprovalTask_Status CHECK (Status IN (N'Pending', N'Completed', N'Cancelled')),
        CONSTRAINT CK_ApprovalTask_Decision CHECK (Decision IS NULL OR Decision IN (N'Approved', N'Rejected'))
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'app.ApprovalTask') AND name = N'IX_ApprovalTask_Assignee_Status')
    CREATE INDEX IX_ApprovalTask_Assignee_Status ON app.ApprovalTask(AssignedEmployeeId, Status, DueOn);
GO

IF OBJECT_ID(N'app.RequestComment', N'U') IS NULL
BEGIN
    CREATE TABLE app.RequestComment
    (
        RequestCommentId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_RequestComment PRIMARY KEY,
        RequestId int NOT NULL,
        AuthorEmployeeId int NOT NULL,
        CommentText nvarchar(2000) NOT NULL,
        IsPrivate bit NOT NULL CONSTRAINT DF_RequestComment_IsPrivate DEFAULT (0),
        CreatedOn datetime2(0) NOT NULL CONSTRAINT DF_RequestComment_CreatedOn DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_RequestComment_Request FOREIGN KEY (RequestId) REFERENCES app.WorkflowRequest(RequestId),
        CONSTRAINT FK_RequestComment_Author FOREIGN KEY (AuthorEmployeeId) REFERENCES org.Employee(EmployeeId)
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'app.RequestComment') AND name = N'IX_RequestComment_RequestId')
    CREATE INDEX IX_RequestComment_RequestId ON app.RequestComment(RequestId, CreatedOn);
GO

MERGE app.RequestType AS target
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
