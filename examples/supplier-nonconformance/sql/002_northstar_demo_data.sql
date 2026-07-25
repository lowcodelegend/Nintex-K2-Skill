SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

/*
  Deterministic demonstration data for the runnable supplier-nonconformance example.
  Records are isolated by SNC-DEMO identifiers (plus the three prototype references),
  are safe to re-run, and must not be copied into a production deployment.
*/
DECLARE @caseTypeId int=(SELECT CaseTypeId FROM SNC.CaseType WHERE CaseTypeCode=N'SUPPLIER_NONCONFORMANCE');
DECLARE @number int=1;

WHILE @number<=128
BEGIN
 DECLARE @caseNumber nvarchar(40)=CASE @number
  WHEN 1 THEN N'SNC-2026-0148'
  WHEN 2 THEN N'SNC-2026-0146'
  WHEN 3 THEN N'SNC-2026-0141'
  ELSE CONCAT(N'SNC-DEMO-',RIGHT(CONCAT(N'0000',@number),4))
 END;
 DECLARE @supplierId nvarchar(100)=CASE @number%4 WHEN 1 THEN N'APEX' WHEN 2 THEN N'ORION' WHEN 3 THEN N'NEXUS' ELSE N'VERIDIAN' END;
 DECLARE @supplierName nvarchar(200)=CASE @number WHEN 1 THEN N'Apex Precision Metals' ELSE CASE @number%4 WHEN 1 THEN N'Apex Components' WHEN 2 THEN N'Orion Metals' WHEN 3 THEN N'Nexus Supply' ELSE N'Veridian Industrial' END END;
 DECLARE @title nvarchar(200)=CASE @number
  WHEN 1 THEN N'Surface pitting on actuator housing'
  WHEN 2 THEN N'Incorrect hardness certificate'
  WHEN 3 THEN N'Packaging seal integrity failure'
  ELSE CONCAT(N'Supplier quality exception ',RIGHT(CONCAT(N'0000',@number),4))
 END;
 DECLARE @stage nvarchar(50)=CASE
  WHEN @number<=34 THEN N'VALIDATE'
  WHEN @number<=63 THEN N'INVESTIGATE'
  WHEN @number<=87 THEN N'RESOLVE'
  WHEN @number<=108 THEN N'DECIDE'
  ELSE N'CLOSE'
 END;
 DECLARE @sla nvarchar(40)=CASE WHEN @number=1 THEN N'Breached' WHEN @number<=12 THEN N'AtRisk' ELSE N'OnTrack' END;
 DECLARE @risk nvarchar(50)=CASE WHEN @number<=18 THEN N'High' WHEN @number<=56 THEN N'Medium' ELSE N'Low' END;
 DECLARE @opened datetime2(0)=DATEADD(day,-((@number*5)%29),DATEADD(hour,-(@number%18),SYSUTCDATETIME()));
 DECLARE @target datetime2(0)=CASE
  WHEN @number=1 THEN DATEADD(hour,-4,SYSUTCDATETIME())
  WHEN @number=2 THEN DATEADD(hour,2,SYSUTCDATETIME())
  WHEN @number=3 THEN DATEADD(hour,7,SYSUTCDATETIME())
  ELSE DATEADD(day,(@number%14)+1,SYSUTCDATETIME())
 END;

 IF NOT EXISTS(SELECT 1 FROM SNC.[Case] WHERE CaseNumber=@caseNumber)
 BEGIN
  INSERT SNC.[Case]
  (CaseNumber,CaseTypeId,Title,Description,Source,Status,CurrentStageCode,PriorityCode,SeverityCode,RiskCode,
   ConfidentialityCode,OwningTeam,OwnerFQN,OpenedDate,TargetDate,LastUpdatedDate,StageEnteredDate,SLAStatus,ConfigurationVersion)
  VALUES
  (@caseNumber,@caseTypeId,@title,N'Deterministic Northstar dashboard demonstration record.',N'Northstar Demo',N'Active',@stage,
   CASE WHEN @number<=12 THEN N'High' ELSE N'Normal' END,N'Major',@risk,N'Internal',N'Supplier Quality',
   N'K2:TRIALS\Administrator',@opened,@target,SYSUTCDATETIME(),@opened,@sla,N'demo-1');

  DECLARE @newCaseId int=SCOPE_IDENTITY();
  INSERT SNC.NonconformanceDetail
  (CaseId,SupplierId,SupplierName,PartNumber,LotNumber,QuantityAffected,SpecificationReference,ContainmentRequired,ContainmentSummary)
  VALUES(@newCaseId,@supplierId,@supplierName,CONCAT(N'PART-',100+(@number%24)),CONCAT(N'LOT-',@number),1+(@number%40),N'Northstar demo specification',1,N'Demo containment is in progress.');
 END;

 SET @number+=1;
END;
GO

;WITH OrderedDemo AS
(
 SELECT c.CaseId,ROW_NUMBER() OVER(ORDER BY c.CaseId) RowNumber
 FROM SNC.[Case] c
 WHERE c.Status NOT IN (N'Closed',N'Cancelled') AND c.Source=N'Northstar Demo'
)
UPDATE c SET CurrentStageCode=CASE
 WHEN d.RowNumber<=18 THEN N'VALIDATE'
 WHEN d.RowNumber<=29 THEN N'CONTAIN'
 WHEN d.RowNumber<=56 THEN N'INVESTIGATE'
 WHEN d.RowNumber<=65 THEN N'REVIEW'
 WHEN d.RowNumber<=101 THEN N'CORRECTIVE_ACTION'
 ELSE N'CLOSE' END
FROM SNC.[Case] c
JOIN OrderedDemo d ON d.CaseId=c.CaseId;
GO

UPDATE c SET
 Title=CASE c.CaseNumber
  WHEN N'SNC-2026-0148' THEN N'Surface pitting on actuator housing'
  WHEN N'SNC-2026-0146' THEN N'Incorrect hardness certificate'
  WHEN N'SNC-2026-0141' THEN N'Packaging seal integrity failure'
 END,
 SLAStatus=CASE WHEN c.CaseNumber=N'SNC-2026-0148' THEN N'Breached' ELSE N'AtRisk' END,
 TargetDate=CASE c.CaseNumber
  WHEN N'SNC-2026-0148' THEN DATEADD(hour,-4,SYSUTCDATETIME())
  WHEN N'SNC-2026-0146' THEN DATEADD(hour,2,SYSUTCDATETIME())
  WHEN N'SNC-2026-0141' THEN DATEADD(hour,7,SYSUTCDATETIME())
 END
FROM SNC.[Case] c
WHERE c.CaseNumber IN (N'SNC-2026-0148',N'SNC-2026-0146',N'SNC-2026-0141');

UPDATE c SET
 SLAStatus=N'AtRisk',
 TargetDate=DATEADD(day,TRY_CONVERT(int,RIGHT(c.CaseNumber,4)),SYSUTCDATETIME())
FROM SNC.[Case] c
WHERE c.CaseNumber IN (
 N'SNC-DEMO-0004',N'SNC-DEMO-0005',N'SNC-DEMO-0006',N'SNC-DEMO-0007',N'SNC-DEMO-0008',
 N'SNC-DEMO-0009',N'SNC-DEMO-0010',N'SNC-DEMO-0011',N'SNC-DEMO-0012'
);

UPDATE n SET SupplierName=CASE c.CaseNumber
 WHEN N'SNC-2026-0148' THEN N'Apex Precision Metals'
 WHEN N'SNC-2026-0146' THEN N'Orion Metals'
 ELSE N'Nexus Supply' END
FROM SNC.NonconformanceDetail n
JOIN SNC.[Case] c ON c.CaseId=n.CaseId
WHERE c.CaseNumber IN (N'SNC-2026-0148',N'SNC-2026-0146',N'SNC-2026-0141');
GO

DECLARE @caseTypeId int=(SELECT CaseTypeId FROM SNC.CaseType WHERE CaseTypeCode=N'SUPPLIER_NONCONFORMANCE');
DECLARE @closedNumber int=1;
WHILE @closedNumber<=40
BEGIN
 DECLARE @caseNumber nvarchar(40)=CONCAT(N'SNC-DEMO-C',RIGHT(CONCAT(N'0000',@closedNumber),4));
 IF NOT EXISTS(SELECT 1 FROM SNC.[Case] WHERE CaseNumber=@caseNumber)
 BEGIN
  DECLARE @closedDate datetime2(0)=DATEADD(day,-((@closedNumber*7)%28),SYSUTCDATETIME());
  INSERT SNC.[Case]
  (CaseNumber,CaseTypeId,Title,Description,Source,Status,CurrentStageCode,PriorityCode,SeverityCode,RiskCode,
   ConfidentialityCode,OwningTeam,OwnerFQN,OpenedDate,TargetDate,ClosedDate,LastUpdatedDate,StageEnteredDate,SLAStatus,
   OutcomeCode,ResolutionSummary,ConfigurationVersion)
  VALUES
  (@caseNumber,@caseTypeId,CONCAT(N'Resolved supplier exception ',RIGHT(CONCAT(N'0000',@closedNumber),4)),
   N'Deterministic resolved record used by the Northstar trend projection.',N'Northstar Demo',N'Closed',N'CLOSE',
   N'Normal',N'Minor',N'Low',N'Internal',N'Supplier Quality',N'K2:TRIALS\Administrator',
   DATEADD(day,-(3+(@closedNumber%10)),@closedDate),@closedDate,@closedDate,@closedDate,@closedDate,N'OnTrack',
   N'RESOLVED',N'Closed after verified corrective action.',N'demo-1');
 END;
 SET @closedNumber+=1;
END;
GO

DECLARE @actionNumber int=1;
WHILE @actionNumber<=7
BEGIN
 DECLARE @caseId int=(
  SELECT CaseId FROM SNC.[Case]
  WHERE CaseNumber=CASE @actionNumber
   WHEN 1 THEN N'SNC-2026-0148'
   WHEN 2 THEN N'SNC-2026-0146'
   WHEN 3 THEN N'SNC-2026-0141'
   ELSE CONCAT(N'SNC-DEMO-',RIGHT(CONCAT(N'0000',@actionNumber),4))
  END
 );
 IF @caseId IS NOT NULL AND NOT EXISTS(
  SELECT 1 FROM SNC.CorrectiveAction WHERE CaseId=@caseId AND ActionTypeCode=N'NORTHSTAR_DEMO'
 )
  INSERT SNC.CorrectiveAction(CaseId,ActionTypeCode,Description,OwnerFQN,DueDate,Status)
  VALUES(@caseId,N'NORTHSTAR_DEMO',N'Demonstration overdue corrective action.',N'K2:TRIALS\Administrator',DATEADD(day,-@actionNumber,SYSUTCDATETIME()),N'Open');
 SET @actionNumber+=1;
END;
GO

MERGE SNC.SupplierQualitySnapshot AS target
USING (VALUES
 (N'APEX',N'Apex Precision Metals',CAST(91.0 AS decimal(5,2)),6,2,62,N'Risk rising · 6 cases'),
 (N'ORION',N'Orion Forge Ltd',CAST(94.2 AS decimal(5,2)),3,0,78,N'Stable · 3 cases'),
 (N'NEXUS',N'Nexus Polymers',CAST(97.4 AS decimal(5,2)),2,0,91,N'Improving · 2 cases')
) source(SupplierId,SupplierName,FirstPassYieldPercent,ActiveCaseCount,RecurrenceCount,SignalScore,SignalLabel)
ON target.SupplierId=source.SupplierId AND target.EffectiveDate=CONVERT(date,SYSUTCDATETIME())
WHEN MATCHED THEN UPDATE SET
 SupplierName=source.SupplierName,
 FirstPassYieldPercent=source.FirstPassYieldPercent,
 ActiveCaseCount=source.ActiveCaseCount,
 RecurrenceCount=source.RecurrenceCount,
 SignalScore=source.SignalScore,
 SignalLabel=source.SignalLabel,
 ConfigurationVersion=N'demo-1'
WHEN NOT MATCHED THEN INSERT
 (SupplierId,SupplierName,FirstPassYieldPercent,ActiveCaseCount,RecurrenceCount,SignalScore,SignalLabel,EffectiveDate,ConfigurationVersion)
 VALUES(source.SupplierId,source.SupplierName,source.FirstPassYieldPercent,source.ActiveCaseCount,source.RecurrenceCount,source.SignalScore,source.SignalLabel,CONVERT(date,SYSUTCDATETIME()),N'demo-1');
GO
