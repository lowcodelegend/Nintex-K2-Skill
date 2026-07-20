SET NOCOUNT ON;
GO

CREATE OR ALTER VIEW CWF.RequestSummary
AS
    SELECT
        r.RequestId,
        r.RequestNumber,
        rt.RequestTypeCode,
        rt.RequestTypeName,
        r.Title,
        r.Amount,
        r.Priority,
        r.Status,
        e.EmployeeNumber AS RequestorEmployeeNumber,
        e.DisplayName AS RequestorName,
        d.DepartmentCode,
        d.DepartmentName,
        r.SubmittedOn,
        r.CompletedOn,
        r.CreatedOn,
        COUNT(t.ApprovalTaskId) AS ApprovalStepCount,
        SUM(CASE WHEN t.Status = N'Pending' THEN 1 ELSE 0 END) AS PendingStepCount
    FROM CWF.WorkflowRequest AS r
    INNER JOIN CWF.RequestType AS rt ON rt.RequestTypeId = r.RequestTypeId
    INNER JOIN CWF.Employee AS e ON e.EmployeeId = r.RequestorEmployeeId
    INNER JOIN CWF.Department AS d ON d.DepartmentId = r.DepartmentId
    LEFT JOIN CWF.ApprovalTask AS t ON t.RequestId = r.RequestId
    GROUP BY
        r.RequestId, r.RequestNumber, rt.RequestTypeCode, rt.RequestTypeName,
        r.Title, r.Amount, r.Priority, r.Status, e.EmployeeNumber, e.DisplayName,
        d.DepartmentCode, d.DepartmentName, r.SubmittedOn, r.CompletedOn, r.CreatedOn;
GO

CREATE OR ALTER VIEW CWF.ApprovalInbox
AS
    SELECT
        t.ApprovalTaskId,
        t.RequestId,
        r.RequestNumber,
        r.Title AS RequestTitle,
        r.Priority,
        t.StepNumber,
        t.StepName,
        t.AssignedEmployeeId,
        a.EmployeeNumber AS AssigneeEmployeeNumber,
        a.DisplayName AS AssigneeName,
        q.DisplayName AS RequestorName,
        t.DueOn,
        CASE WHEN t.DueOn < SYSUTCDATETIME() THEN CONVERT(bit, 1) ELSE CONVERT(bit, 0) END AS IsOverdue,
        t.CreatedOn
    FROM CWF.ApprovalTask AS t
    INNER JOIN CWF.WorkflowRequest AS r ON r.RequestId = t.RequestId
    INNER JOIN CWF.Employee AS a ON a.EmployeeId = t.AssignedEmployeeId
    INNER JOIN CWF.Employee AS q ON q.EmployeeId = r.RequestorEmployeeId
    WHERE t.Status = N'Pending';
GO

CREATE OR ALTER PROCEDURE CWF.WorkflowRequest_Submit
    @RequestId int,
    @SubmittedByEmployeeId int
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    DECLARE @ManagerEmployeeId int;
    DECLARE @SlaHours int;

    SELECT
        @ManagerEmployeeId = e.ManagerEmployeeId,
        @SlaHours = rt.DefaultSlaHours
    FROM CWF.WorkflowRequest AS r
    INNER JOIN CWF.Employee AS e ON e.EmployeeId = r.RequestorEmployeeId
    INNER JOIN CWF.RequestType AS rt ON rt.RequestTypeId = r.RequestTypeId
    WHERE r.RequestId = @RequestId
      AND r.RequestorEmployeeId = @SubmittedByEmployeeId
      AND r.Status = N'Draft';

    IF @ManagerEmployeeId IS NULL
        THROW 51000, 'The request is not a submit-ready draft or its requestor has no manager.', 1;

    UPDATE CWF.WorkflowRequest
    SET Status = N'Submitted',
        SubmittedOn = SYSUTCDATETIME(),
        ModifiedOn = SYSUTCDATETIME()
    WHERE RequestId = @RequestId;

    IF NOT EXISTS (SELECT 1 FROM CWF.ApprovalTask WHERE RequestId = @RequestId AND StepNumber = 1)
    BEGIN
        INSERT CWF.ApprovalTask (RequestId, StepNumber, StepName, AssignedEmployeeId, DueOn)
        VALUES (@RequestId, 1, N'Manager approval', @ManagerEmployeeId, DATEADD(hour, @SlaHours, SYSUTCDATETIME()));
    END;

    COMMIT TRANSACTION;

    SELECT RequestId, RequestNumber, Status, SubmittedOn, ModifiedOn
    FROM CWF.WorkflowRequest
    WHERE RequestId = @RequestId;
END;
GO

CREATE OR ALTER PROCEDURE CWF.ApprovalTask_Decide
    @ApprovalTaskId int,
    @ActorEmployeeId int,
    @Decision nvarchar(20),
    @DecisionComment nvarchar(1000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @Decision NOT IN (N'Approved', N'Rejected')
        THROW 51001, 'Decision must be Approved or Rejected.', 1;

    BEGIN TRANSACTION;

    DECLARE @RequestId int;

    SELECT @RequestId = RequestId
    FROM CWF.ApprovalTask
    WHERE ApprovalTaskId = @ApprovalTaskId
      AND AssignedEmployeeId = @ActorEmployeeId
      AND Status = N'Pending';

    IF @RequestId IS NULL
        THROW 51002, 'The pending task was not found for this actor.', 1;

    UPDATE CWF.ApprovalTask
    SET Status = N'Completed',
        Decision = @Decision,
        DecisionComment = @DecisionComment,
        ActedOn = SYSUTCDATETIME()
    WHERE ApprovalTaskId = @ApprovalTaskId;

    UPDATE CWF.WorkflowRequest
    SET Status = CASE WHEN @Decision = N'Approved' THEN N'Approved' ELSE N'Rejected' END,
        CompletedOn = SYSUTCDATETIME(),
        ModifiedOn = SYSUTCDATETIME()
    WHERE RequestId = @RequestId;

    COMMIT TRANSACTION;

    SELECT
        t.ApprovalTaskId,
        t.RequestId,
        t.Status AS TaskStatus,
        t.Decision,
        t.ActedOn,
        r.Status AS RequestStatus,
        r.CompletedOn
    FROM CWF.ApprovalTask AS t
    INNER JOIN CWF.WorkflowRequest AS r ON r.RequestId = t.RequestId
    WHERE t.ApprovalTaskId = @ApprovalTaskId;
END;
GO

CREATE OR ALTER PROCEDURE CWF.WorkflowDashboard
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        Status,
        COUNT(*) AS RequestCount,
        COALESCE(SUM(Amount), 0) AS TotalAmount,
        MIN(CreatedOn) AS OldestRequestCreatedOn,
        MAX(CreatedOn) AS NewestRequestCreatedOn
    FROM CWF.WorkflowRequest
    GROUP BY Status;
END;
GO

IF NOT EXISTS (SELECT 1 FROM CWF.WorkflowRequest WHERE RequestNumber = N'CWF-TEST-0001')
BEGIN
    INSERT CWF.WorkflowRequest
    (
        RequestNumber, RequestTypeId, RequestorEmployeeId, DepartmentId,
        Title, Description, Amount, Priority, Status, SubmittedOn
    )
    SELECT
        N'CWF-TEST-0001', rt.RequestTypeId, e.EmployeeId, e.DepartmentId,
        N'Laptop purchase approval', N'Test request for a standard corporate approval workflow.',
        2450.00, N'High', N'Submitted', SYSUTCDATETIME()
    FROM CWF.RequestType AS rt
    CROSS JOIN CWF.Employee AS e
    WHERE rt.RequestTypeCode = N'PURCHASE' AND e.EmployeeNumber = N'E1002';
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM CWF.ApprovalTask AS t
    INNER JOIN CWF.WorkflowRequest AS r ON r.RequestId = t.RequestId
    WHERE r.RequestNumber = N'CWF-TEST-0001' AND t.StepNumber = 1
)
BEGIN
    INSERT CWF.ApprovalTask (RequestId, StepNumber, StepName, AssignedEmployeeId, DueOn)
    SELECT r.RequestId, 1, N'Manager approval', m.EmployeeId, DATEADD(hour, 48, SYSUTCDATETIME())
    FROM CWF.WorkflowRequest AS r
    CROSS JOIN CWF.Employee AS m
    WHERE r.RequestNumber = N'CWF-TEST-0001' AND m.EmployeeNumber = N'E1001';
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM CWF.RequestComment AS c
    INNER JOIN CWF.WorkflowRequest AS r ON r.RequestId = c.RequestId
    WHERE r.RequestNumber = N'CWF-TEST-0001' AND c.CommentText = N'Initial test request submitted for manager approval.'
)
BEGIN
    INSERT CWF.RequestComment (RequestId, AuthorEmployeeId, CommentText)
    SELECT r.RequestId, e.EmployeeId, N'Initial test request submitted for manager approval.'
    FROM CWF.WorkflowRequest AS r
    CROSS JOIN CWF.Employee AS e
    WHERE r.RequestNumber = N'CWF-TEST-0001' AND e.EmployeeNumber = N'E1002';
END;
GO
