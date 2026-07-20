SET NOCOUNT ON;

IF SCHEMA_ID(N'app') IS NULL
    EXEC(N'CREATE SCHEMA app AUTHORIZATION dbo');
GO

IF OBJECT_ID(N'app.Request', N'U') IS NULL
BEGIN
    CREATE TABLE app.Request
    (
        RequestId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Request PRIMARY KEY,
        Title nvarchar(200) NOT NULL,
        Amount decimal(18,2) NOT NULL CONSTRAINT DF_Request_Amount DEFAULT (0),
        Status nvarchar(30) NOT NULL CONSTRAINT DF_Request_Status DEFAULT (N'Draft'),
        RequestedBy nvarchar(256) NOT NULL,
        CreatedOn datetime2(0) NOT NULL CONSTRAINT DF_Request_CreatedOn DEFAULT (SYSUTCDATETIME()),
        ModifiedOn datetime2(0) NOT NULL CONSTRAINT DF_Request_ModifiedOn DEFAULT (SYSUTCDATETIME()),
        RowVersion rowversion NOT NULL
    );
END;
GO

IF OBJECT_ID(N'app.RequestLine', N'U') IS NULL
BEGIN
    CREATE TABLE app.RequestLine
    (
        RequestLineId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_RequestLine PRIMARY KEY,
        RequestId int NOT NULL,
        Description nvarchar(300) NOT NULL,
        Quantity decimal(18,4) NOT NULL CONSTRAINT DF_RequestLine_Quantity DEFAULT (1),
        UnitPrice decimal(18,2) NOT NULL CONSTRAINT DF_RequestLine_UnitPrice DEFAULT (0),
        CONSTRAINT FK_RequestLine_Request FOREIGN KEY (RequestId) REFERENCES app.Request(RequestId)
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'app.RequestLine') AND name = N'IX_RequestLine_RequestId')
    CREATE INDEX IX_RequestLine_RequestId ON app.RequestLine(RequestId);
GO

CREATE OR ALTER VIEW app.OpenRequest
AS
    SELECT
        r.RequestId,
        r.Title,
        r.Amount,
        r.Status,
        r.RequestedBy,
        r.CreatedOn,
        COUNT(rl.RequestLineId) AS LineCount
    FROM app.Request AS r
    LEFT JOIN app.RequestLine AS rl ON rl.RequestId = r.RequestId
    WHERE r.Status IN (N'Draft', N'Submitted')
    GROUP BY r.RequestId, r.Title, r.Amount, r.Status, r.RequestedBy, r.CreatedOn;
GO

CREATE OR ALTER PROCEDURE app.Request_Create
    @Title nvarchar(200),
    @Amount decimal(18,2),
    @RequestedBy nvarchar(256)
AS
BEGIN
    SET NOCOUNT ON;

    INSERT app.Request (Title, Amount, Status, RequestedBy)
    VALUES (@Title, @Amount, N'Draft', @RequestedBy);

    SELECT RequestId, Title, Amount, Status, RequestedBy, CreatedOn, ModifiedOn
    FROM app.Request
    WHERE RequestId = CONVERT(int, SCOPE_IDENTITY());
END;
GO

CREATE OR ALTER PROCEDURE app.Request_Get
    @RequestId int
AS
BEGIN
    SET NOCOUNT ON;

    SELECT RequestId, Title, Amount, Status, RequestedBy, CreatedOn, ModifiedOn
    FROM app.Request
    WHERE RequestId = @RequestId;
END;
GO

IF NOT EXISTS (SELECT 1 FROM app.Request WHERE Title = N'CLI smoke test')
BEGIN
    INSERT app.Request (Title, Amount, Status, RequestedBy)
    VALUES (N'CLI smoke test', 125.50, N'Draft', N'TRIALS\Administrator');
END;
GO
