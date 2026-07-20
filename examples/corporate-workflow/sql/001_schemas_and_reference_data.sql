SET NOCOUNT ON;

IF SCHEMA_ID(N'CWF') IS NULL
    EXEC(N'CREATE SCHEMA CWF AUTHORIZATION dbo');
GO

IF OBJECT_ID(N'CWF.Department', N'U') IS NULL
BEGIN
    CREATE TABLE CWF.Department
    (
        DepartmentId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Department PRIMARY KEY,
        DepartmentCode nvarchar(20) NOT NULL CONSTRAINT UQ_Department_DepartmentCode UNIQUE,
        DepartmentName nvarchar(100) NOT NULL,
        CostCentre nvarchar(30) NULL,
        IsActive bit NOT NULL CONSTRAINT DF_Department_IsActive DEFAULT (1),
        CreatedOn datetime2(0) NOT NULL CONSTRAINT DF_Department_CreatedOn DEFAULT (SYSUTCDATETIME())
    );
END;
GO

IF OBJECT_ID(N'CWF.Employee', N'U') IS NULL
BEGIN
    CREATE TABLE CWF.Employee
    (
        EmployeeId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Employee PRIMARY KEY,
        EmployeeNumber nvarchar(30) NOT NULL CONSTRAINT UQ_Employee_EmployeeNumber UNIQUE,
        DisplayName nvarchar(150) NOT NULL,
        EmailAddress nvarchar(256) NOT NULL CONSTRAINT UQ_Employee_EmailAddress UNIQUE,
        DepartmentId int NOT NULL,
        ManagerEmployeeId int NULL,
        JobTitle nvarchar(120) NULL,
        IsActive bit NOT NULL CONSTRAINT DF_Employee_IsActive DEFAULT (1),
        CreatedOn datetime2(0) NOT NULL CONSTRAINT DF_Employee_CreatedOn DEFAULT (SYSUTCDATETIME()),
        ModifiedOn datetime2(0) NOT NULL CONSTRAINT DF_Employee_ModifiedOn DEFAULT (SYSUTCDATETIME()),
        RowVersion rowversion NOT NULL,
        CONSTRAINT FK_Employee_Department FOREIGN KEY (DepartmentId) REFERENCES CWF.Department(DepartmentId),
        CONSTRAINT FK_Employee_Manager FOREIGN KEY (ManagerEmployeeId) REFERENCES CWF.Employee(EmployeeId)
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'CWF.Employee') AND name = N'IX_Employee_DepartmentId')
    CREATE INDEX IX_Employee_DepartmentId ON CWF.Employee(DepartmentId);
GO

MERGE CWF.Department AS target
USING
(
    VALUES
        (N'FIN', N'Finance', N'CC-100'),
        (N'HR', N'Human Resources', N'CC-200'),
        (N'IT', N'Information Technology', N'CC-300'),
        (N'OPS', N'Operations', N'CC-400')
) AS source (DepartmentCode, DepartmentName, CostCentre)
ON target.DepartmentCode = source.DepartmentCode
WHEN MATCHED THEN
    UPDATE SET DepartmentName = source.DepartmentName, CostCentre = source.CostCentre, IsActive = 1
WHEN NOT MATCHED THEN
    INSERT (DepartmentCode, DepartmentName, CostCentre)
    VALUES (source.DepartmentCode, source.DepartmentName, source.CostCentre);
GO

IF NOT EXISTS (SELECT 1 FROM CWF.Employee WHERE EmployeeNumber = N'E1001')
BEGIN
    INSERT CWF.Employee (EmployeeNumber, DisplayName, EmailAddress, DepartmentId, JobTitle)
    SELECT N'E1001', N'Maya Finance', N'maya.finance@example.test', DepartmentId, N'Finance Manager'
    FROM CWF.Department WHERE DepartmentCode = N'FIN';
END;
GO

IF NOT EXISTS (SELECT 1 FROM CWF.Employee WHERE EmployeeNumber = N'E1002')
BEGIN
    INSERT CWF.Employee (EmployeeNumber, DisplayName, EmailAddress, DepartmentId, ManagerEmployeeId, JobTitle)
    SELECT N'E1002', N'Alex Requestor', N'alex.requestor@example.test', d.DepartmentId, m.EmployeeId, N'Financial Analyst'
    FROM CWF.Department AS d
    CROSS JOIN CWF.Employee AS m
    WHERE d.DepartmentCode = N'FIN' AND m.EmployeeNumber = N'E1001';
END;
GO

IF NOT EXISTS (SELECT 1 FROM CWF.Employee WHERE EmployeeNumber = N'E3001')
BEGIN
    INSERT CWF.Employee (EmployeeNumber, DisplayName, EmailAddress, DepartmentId, JobTitle)
    SELECT N'E3001', N'Jordan IT', N'jordan.it@example.test', DepartmentId, N'IT Manager'
    FROM CWF.Department WHERE DepartmentCode = N'IT';
END;
GO
