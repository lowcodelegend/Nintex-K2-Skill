SET NOCOUNT ON;

IF SCHEMA_ID(N'LPR') IS NULL
    EXEC(N'CREATE SCHEMA LPR AUTHORIZATION dbo');
GO

IF OBJECT_ID(N'LPR.LeaveType', N'U') IS NULL
BEGIN
    CREATE TABLE LPR.LeaveType
    (
        LeaveTypeCode nvarchar(20) NOT NULL CONSTRAINT PK_LeaveType PRIMARY KEY,
        LeaveTypeName nvarchar(100) NOT NULL,
        Description nvarchar(500) NULL,
        SortOrder int NOT NULL,
        IsActive bit NOT NULL CONSTRAINT DF_LeaveType_IsActive DEFAULT (1)
    );
END;
GO

MERGE LPR.LeaveType AS target
USING
(
    VALUES
        (N'ANNUAL', N'Annual leave', N'Planned annual vacation leave.', 10, CONVERT(bit, 1)),
        (N'SICK', N'Sick leave', N'Leave for illness or medical care.', 20, CONVERT(bit, 1)),
        (N'COMPASSIONATE', N'Compassionate leave', N'Leave for urgent family circumstances.', 30, CONVERT(bit, 1)),
        (N'UNPAID', N'Unpaid leave', N'Approved leave without pay.', 40, CONVERT(bit, 1))
) AS source (LeaveTypeCode, LeaveTypeName, Description, SortOrder, IsActive)
ON target.LeaveTypeCode = source.LeaveTypeCode
WHEN MATCHED THEN
    UPDATE SET
        LeaveTypeName = source.LeaveTypeName,
        Description = source.Description,
        SortOrder = source.SortOrder
WHEN NOT MATCHED THEN
    INSERT (LeaveTypeCode, LeaveTypeName, Description, SortOrder, IsActive)
    VALUES (source.LeaveTypeCode, source.LeaveTypeName, source.Description, source.SortOrder, source.IsActive);
GO

IF OBJECT_ID(N'LPR.LeaveStatus', N'U') IS NULL
BEGIN
    CREATE TABLE LPR.LeaveStatus
    (
        StatusCode nvarchar(30) NOT NULL CONSTRAINT PK_LeaveStatus PRIMARY KEY,
        StatusName nvarchar(100) NOT NULL,
        SortOrder int NOT NULL
    );
END;
GO

MERGE LPR.LeaveStatus AS target
USING
(
    VALUES
        (N'Draft', N'Draft', 10),
        (N'Pending Approval', N'Pending Approval', 20),
        (N'Approved', N'Approved', 30),
        (N'Rejected', N'Rejected', 40)
) AS source (StatusCode, StatusName, SortOrder)
ON target.StatusCode = source.StatusCode
WHEN MATCHED THEN
    UPDATE SET StatusName = source.StatusName, SortOrder = source.SortOrder
WHEN NOT MATCHED THEN
    INSERT (StatusCode, StatusName, SortOrder)
    VALUES (source.StatusCode, source.StatusName, source.SortOrder);
GO

IF OBJECT_ID(N'LPR.LeaveRequest', N'U') IS NULL
BEGIN
    CREATE TABLE LPR.LeaveRequest
    (
        LeaveRequestId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_LeaveRequest PRIMARY KEY,
        EmployeeName nvarchar(150) NOT NULL,
        LeaveTypeCode nvarchar(20) NOT NULL,
        StartDate date NOT NULL,
        EndDate date NOT NULL,
        DaysRequested decimal(6,2) NOT NULL,
        Reason nvarchar(2000) NOT NULL,
        Status nvarchar(30) NOT NULL CONSTRAINT DF_LeaveRequest_Status DEFAULT (N'Draft'),
        SubmittedOn datetime2(0) NULL,
        DecisionOn datetime2(0) NULL,
        CreatedOn datetime2(0) NULL CONSTRAINT DF_LeaveRequest_CreatedOn DEFAULT (SYSUTCDATETIME()),
        ModifiedOn datetime2(0) NULL CONSTRAINT DF_LeaveRequest_ModifiedOn DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_LeaveRequest_LeaveType FOREIGN KEY (LeaveTypeCode)
            REFERENCES LPR.LeaveType(LeaveTypeCode),
        CONSTRAINT FK_LeaveRequest_Status FOREIGN KEY (Status)
            REFERENCES LPR.LeaveStatus(StatusCode),
        CONSTRAINT CK_LeaveRequest_DateRange CHECK (EndDate >= StartDate),
        CONSTRAINT CK_LeaveRequest_DaysRequested CHECK (DaysRequested > 0)
    );
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'LPR.LeaveRequest')
      AND name = N'IX_LeaveRequest_Status_StartDate'
)
    CREATE INDEX IX_LeaveRequest_Status_StartDate
        ON LPR.LeaveRequest(Status, StartDate);
GO
