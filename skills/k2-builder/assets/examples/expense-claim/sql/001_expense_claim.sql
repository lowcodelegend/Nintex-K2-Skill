IF SCHEMA_ID(N'EXP') IS NULL EXEC(N'CREATE SCHEMA EXP AUTHORIZATION dbo');
GO

IF OBJECT_ID(N'EXP.ExpenseCategory', N'U') IS NULL
BEGIN
    CREATE TABLE EXP.ExpenseCategory
    (
        CategoryCode nvarchar(30) NOT NULL CONSTRAINT PK_EXP_ExpenseCategory PRIMARY KEY,
        CategoryName nvarchar(100) NOT NULL,
        IsActive bit NOT NULL CONSTRAINT DF_EXP_ExpenseCategory_IsActive DEFAULT (1),
        SortOrder int NOT NULL CONSTRAINT DF_EXP_ExpenseCategory_SortOrder DEFAULT (100),
        CONSTRAINT CK_EXP_ExpenseCategory_SortOrder CHECK (SortOrder >= 0)
    );
END;
GO

MERGE EXP.ExpenseCategory AS target
USING (VALUES
    (N'TRAVEL', N'Travel', 10),
    (N'MEALS', N'Meals', 20),
    (N'LODGING', N'Lodging', 30),
    (N'OTHER', N'Other', 100)
) AS source(CategoryCode, CategoryName, SortOrder)
ON target.CategoryCode = source.CategoryCode
WHEN MATCHED THEN UPDATE SET CategoryName=source.CategoryName, SortOrder=source.SortOrder
WHEN NOT MATCHED THEN INSERT(CategoryCode, CategoryName, SortOrder) VALUES(source.CategoryCode, source.CategoryName, source.SortOrder);
GO

IF OBJECT_ID(N'EXP.ExpenseClaim', N'U') IS NULL
BEGIN
    CREATE TABLE EXP.ExpenseClaim
    (
        ExpenseClaimId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_EXP_ExpenseClaim PRIMARY KEY,
        ClaimTitle nvarchar(200) NOT NULL,
        Department nvarchar(100) NOT NULL,
        BusinessPurpose nvarchar(1000) NULL,
        Status nvarchar(30) NOT NULL CONSTRAINT DF_EXP_ExpenseClaim_Status DEFAULT (N'Draft'),
        TotalAmount decimal(18,2) NOT NULL CONSTRAINT DF_EXP_ExpenseClaim_TotalAmount DEFAULT (0),
        CreatedOn datetime2(0) NULL CONSTRAINT DF_EXP_ExpenseClaim_CreatedOn DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT CK_EXP_ExpenseClaim_TotalAmount CHECK (TotalAmount >= 0)
    );
END;
GO

IF OBJECT_ID(N'EXP.ExpenseLine', N'U') IS NULL
BEGIN
    CREATE TABLE EXP.ExpenseLine
    (
        ExpenseLineId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_EXP_ExpenseLine PRIMARY KEY,
        ExpenseClaimId int NOT NULL,
        CategoryCode nvarchar(30) NOT NULL,
        ExpenseDate date NOT NULL,
        Description nvarchar(300) NOT NULL,
        Amount decimal(18,2) NOT NULL,
        ReceiptReference nvarchar(200) NULL,
        CONSTRAINT FK_EXP_ExpenseLine_ExpenseClaim FOREIGN KEY (ExpenseClaimId)
            REFERENCES EXP.ExpenseClaim(ExpenseClaimId) ON DELETE CASCADE,
        CONSTRAINT FK_EXP_ExpenseLine_Category FOREIGN KEY (CategoryCode)
            REFERENCES EXP.ExpenseCategory(CategoryCode),
        CONSTRAINT CK_EXP_ExpenseLine_Amount CHECK (Amount > 0)
    );
    CREATE INDEX IX_EXP_ExpenseLine_ExpenseClaimId ON EXP.ExpenseLine(ExpenseClaimId, ExpenseLineId);
END;
GO

CREATE OR ALTER VIEW EXP.ExpenseClaimSummary
AS
SELECT c.ExpenseClaimId, c.ClaimTitle, c.Department, c.Status,
       COUNT(l.ExpenseLineId) AS LineCount,
       COALESCE(SUM(l.Amount), 0) AS CalculatedTotal
FROM EXP.ExpenseClaim AS c
LEFT JOIN EXP.ExpenseLine AS l ON l.ExpenseClaimId=c.ExpenseClaimId
GROUP BY c.ExpenseClaimId, c.ClaimTitle, c.Department, c.Status;
GO
