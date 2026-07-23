SET NOCOUNT ON;
GO

IF SCHEMA_ID(N'SNC') IS NULL EXEC(N'CREATE SCHEMA SNC AUTHORIZATION dbo');
GO

IF OBJECT_ID(N'SNC.LookupValue', N'U') IS NULL CREATE TABLE SNC.LookupValue
(
    LookupValueId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_SNC_LookupValue PRIMARY KEY,
    LookupType nvarchar(40) NOT NULL,
    ValueCode nvarchar(50) NOT NULL,
    ValueName nvarchar(150) NOT NULL,
    SortOrder int NOT NULL CONSTRAINT DF_SNC_LookupValue_Sort DEFAULT (0),
    IsActive bit NOT NULL CONSTRAINT DF_SNC_LookupValue_Active DEFAULT (1),
    CONSTRAINT UQ_SNC_LookupValue UNIQUE (LookupType, ValueCode)
);
GO

MERGE SNC.LookupValue AS t USING (VALUES
 (N'STATUS',N'Draft',N'Draft',10),(N'STATUS',N'Open',N'Open',20),(N'STATUS',N'Active',N'Active',30),(N'STATUS',N'OnHold',N'On Hold',40),(N'STATUS',N'AwaitingInformation',N'Awaiting Information',50),(N'STATUS',N'Escalated',N'Escalated',60),(N'STATUS',N'Resolved',N'Resolved',70),(N'STATUS',N'Closed',N'Closed',80),(N'STATUS',N'Cancelled',N'Cancelled',90),(N'STATUS',N'Reopened',N'Reopened',100),(N'STATUS',N'Error',N'Error',110),
 (N'PRIORITY',N'Low',N'Low',10),(N'PRIORITY',N'Normal',N'Normal',20),(N'PRIORITY',N'High',N'High',30),(N'PRIORITY',N'Critical',N'Critical',40),
 (N'SEVERITY',N'Minor',N'Minor',10),(N'SEVERITY',N'Major',N'Major',20),(N'SEVERITY',N'Critical',N'Critical',30),
 (N'RISK',N'Low',N'Low',10),(N'RISK',N'Medium',N'Medium',20),(N'RISK',N'High',N'High',30),
 (N'CONFIDENTIALITY',N'Internal',N'Internal',10),(N'CONFIDENTIALITY',N'Restricted',N'Restricted',20),
 (N'EVIDENCE_TYPE',N'OriginatingRecord',N'Originating Record',10),(N'EVIDENCE_TYPE',N'Investigation',N'Investigation Evidence',20),(N'EVIDENCE_TYPE',N'CorrectiveAction',N'Corrective Action Evidence',30),
 (N'DECISION_TYPE',N'Disposition',N'Disposition',10),(N'COMMUNICATION_TYPE',N'Email',N'Email',10)
) AS s(LookupType,ValueCode,ValueName,SortOrder)
ON t.LookupType=s.LookupType AND t.ValueCode=s.ValueCode
WHEN MATCHED THEN UPDATE SET ValueName=s.ValueName,SortOrder=s.SortOrder,IsActive=1
WHEN NOT MATCHED THEN INSERT(LookupType,ValueCode,ValueName,SortOrder) VALUES(s.LookupType,s.ValueCode,s.ValueName,s.SortOrder);
GO

IF OBJECT_ID(N'SNC.SLAProfile', N'U') IS NULL CREATE TABLE SNC.SLAProfile
(
 SLAProfileId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_SNC_SLAProfile PRIMARY KEY,
 ProfileCode nvarchar(50) NOT NULL CONSTRAINT UQ_SNC_SLAProfile UNIQUE,
 Name nvarchar(150) NOT NULL, TimeBasis nvarchar(20) NOT NULL CONSTRAINT DF_SNC_SLA_TimeBasis DEFAULT(N'UTC'),
 ResponseHours int NOT NULL, ResolutionHours int NOT NULL, WarningPercent int NOT NULL,
 BusinessCalendarCode nvarchar(50) NOT NULL, IsActive bit NOT NULL CONSTRAINT DF_SNC_SLA_Active DEFAULT(1)
);
GO
MERGE SNC.SLAProfile AS t USING (VALUES(N'SNC_STANDARD',N'Standard nonconformance SLA',8,80,80,N'DEFAULT_BUSINESS')) s(ProfileCode,Name,ResponseHours,ResolutionHours,WarningPercent,BusinessCalendarCode)
ON t.ProfileCode=s.ProfileCode WHEN MATCHED THEN UPDATE SET Name=s.Name,ResponseHours=s.ResponseHours,ResolutionHours=s.ResolutionHours,WarningPercent=s.WarningPercent,BusinessCalendarCode=s.BusinessCalendarCode,IsActive=1
WHEN NOT MATCHED THEN INSERT(ProfileCode,Name,ResponseHours,ResolutionHours,WarningPercent,BusinessCalendarCode) VALUES(s.ProfileCode,s.Name,s.ResponseHours,s.ResolutionHours,s.WarningPercent,s.BusinessCalendarCode);
GO

IF OBJECT_ID(N'SNC.CaseType', N'U') IS NULL CREATE TABLE SNC.CaseType
(
 CaseTypeId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_SNC_CaseType PRIMARY KEY,
 CaseTypeCode nvarchar(50) NOT NULL CONSTRAINT UQ_SNC_CaseType UNIQUE, Name nvarchar(150) NOT NULL, Description nvarchar(1000) NULL,
 IsActive bit NOT NULL CONSTRAINT DF_SNC_CaseType_Active DEFAULT(1), InitialStageCode nvarchar(50) NOT NULL,
 DefaultPriorityCode nvarchar(50) NOT NULL, DefaultSLAProfileId int NOT NULL, WorkflowVersion nvarchar(30) NOT NULL,
 ConfigurationVersion nvarchar(30) NOT NULL, AIEnabled bit NOT NULL CONSTRAINT DF_SNC_CaseType_AI DEFAULT(0), RetentionCode nvarchar(50) NOT NULL,
 CONSTRAINT FK_SNC_CaseType_SLA FOREIGN KEY(DefaultSLAProfileId) REFERENCES SNC.SLAProfile(SLAProfileId)
);
GO
MERGE SNC.CaseType AS t USING (SELECT N'SUPPLIER_NONCONFORMANCE' CaseTypeCode,N'Supplier Nonconformance' Name,N'Evidence-driven supplier quality nonconformance' Description,N'CAPTURE' InitialStageCode,N'Normal' DefaultPriorityCode,SLAProfileId,N'1' WorkflowVersion,N'1' ConfigurationVersion,CONVERT(bit,0) AIEnabled,N'QUALITY_RECORD' RetentionCode FROM SNC.SLAProfile WHERE ProfileCode=N'SNC_STANDARD') s
ON t.CaseTypeCode=s.CaseTypeCode WHEN MATCHED THEN UPDATE SET Name=s.Name,Description=s.Description,InitialStageCode=s.InitialStageCode,DefaultPriorityCode=s.DefaultPriorityCode,DefaultSLAProfileId=s.SLAProfileId,WorkflowVersion=s.WorkflowVersion,ConfigurationVersion=s.ConfigurationVersion,AIEnabled=s.AIEnabled,RetentionCode=s.RetentionCode,IsActive=1
WHEN NOT MATCHED THEN INSERT(CaseTypeCode,Name,Description,InitialStageCode,DefaultPriorityCode,DefaultSLAProfileId,WorkflowVersion,ConfigurationVersion,AIEnabled,RetentionCode) VALUES(s.CaseTypeCode,s.Name,s.Description,s.InitialStageCode,s.DefaultPriorityCode,s.SLAProfileId,s.WorkflowVersion,s.ConfigurationVersion,s.AIEnabled,s.RetentionCode);
GO

IF OBJECT_ID(N'SNC.StageDefinition', N'U') IS NULL CREATE TABLE SNC.StageDefinition
(
 StageDefinitionId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_SNC_StageDefinition PRIMARY KEY, CaseTypeId int NOT NULL,
 StageCode nvarchar(50) NOT NULL, Name nvarchar(150) NOT NULL, Sequence int NOT NULL, StageWorkflowName nvarchar(200) NOT NULL,
 EntryRuleCode nvarchar(50) NULL, ExitRuleCode nvarchar(50) NOT NULL, SLAProfileId int NOT NULL, AssignmentRuleCode nvarchar(50) NULL,
 AllowReentry bit NOT NULL, AllowSkip bit NOT NULL, RequiresHumanCompletion bit NOT NULL, IsTerminal bit NOT NULL, IsActive bit NOT NULL,
 CONSTRAINT UQ_SNC_StageDefinition UNIQUE(CaseTypeId,StageCode), CONSTRAINT FK_SNC_StageDefinition_CaseType FOREIGN KEY(CaseTypeId) REFERENCES SNC.CaseType(CaseTypeId),
 CONSTRAINT FK_SNC_StageDefinition_SLA FOREIGN KEY(SLAProfileId) REFERENCES SNC.SLAProfile(SLAProfileId)
);
GO
MERGE SNC.StageDefinition AS t USING
(SELECT ct.CaseTypeId,v.StageCode,v.Name,v.Sequence,v.StageWorkflowName,v.ExitRuleCode,sp.SLAProfileId,v.AllowReentry,v.IsTerminal FROM SNC.CaseType ct CROSS JOIN SNC.SLAProfile sp CROSS APPLY(VALUES
(N'CAPTURE',N'Capture',10,N'SNC.Stage Capture',N'CAPTURE_COMPLETE',1,0),(N'VALIDATE',N'Validate',20,N'SNC.Stage Validate',N'VALIDATION_COMPLETE',1,0),
(N'CLASSIFY',N'Classify and Prioritise',30,N'SNC.Stage Classify',N'CLASSIFICATION_COMPLETE',0,0),(N'ASSIGN',N'Assign and Route',40,N'SNC.Stage Assign',N'ASSIGNMENT_COMPLETE',0,0),
(N'INVESTIGATE',N'Investigate or Fulfil',50,N'SNC.Stage Investigate',N'EVIDENCE_COMPLETE',1,0),(N'DECIDE',N'Review and Decide',60,N'SNC.Stage Decide',N'DECISION_RECORDED',0,0),
(N'RESOLVE',N'Resolve and Communicate',70,N'SNC.Stage Resolve',N'RESOLUTION_COMPLETE',0,0),(N'CLOSE',N'Close and Learn',80,N'SNC.Stage Close',N'CLOSURE_COMPLETE',0,1)
)v(StageCode,Name,Sequence,StageWorkflowName,ExitRuleCode,AllowReentry,IsTerminal) WHERE ct.CaseTypeCode=N'SUPPLIER_NONCONFORMANCE' AND sp.ProfileCode=N'SNC_STANDARD')s
ON t.CaseTypeId=s.CaseTypeId AND t.StageCode=s.StageCode WHEN MATCHED THEN UPDATE SET Name=s.Name,Sequence=s.Sequence,StageWorkflowName=s.StageWorkflowName,ExitRuleCode=s.ExitRuleCode,SLAProfileId=s.SLAProfileId,AllowReentry=s.AllowReentry,IsTerminal=s.IsTerminal,IsActive=1
WHEN NOT MATCHED THEN INSERT(CaseTypeId,StageCode,Name,Sequence,StageWorkflowName,ExitRuleCode,SLAProfileId,AllowReentry,AllowSkip,RequiresHumanCompletion,IsTerminal,IsActive) VALUES(s.CaseTypeId,s.StageCode,s.Name,s.Sequence,s.StageWorkflowName,s.ExitRuleCode,s.SLAProfileId,s.AllowReentry,0,1,s.IsTerminal,1);
GO

IF OBJECT_ID(N'SNC.AllowedTransition', N'U') IS NULL CREATE TABLE SNC.AllowedTransition
(
 TransitionId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_SNC_AllowedTransition PRIMARY KEY, CaseTypeId int NOT NULL,
 FromStageCode nvarchar(50) NOT NULL, OutcomeCode nvarchar(50) NOT NULL, ToStageCode nvarchar(50) NOT NULL,
 GuardRuleCode nvarchar(50) NULL, ApprovalRuleCode nvarchar(50) NULL, IsReopen bit NOT NULL, IsReentry bit NOT NULL, IsActive bit NOT NULL,
 CONSTRAINT UQ_SNC_AllowedTransition UNIQUE(CaseTypeId,FromStageCode,OutcomeCode,ToStageCode), CONSTRAINT FK_SNC_AllowedTransition_CaseType FOREIGN KEY(CaseTypeId) REFERENCES SNC.CaseType(CaseTypeId)
);
GO
MERGE SNC.AllowedTransition AS t USING (SELECT ct.CaseTypeId,v.* FROM SNC.CaseType ct CROSS APPLY(VALUES
(N'CAPTURE',N'SUBMITTED',N'VALIDATE',0,0),(N'VALIDATE',N'VALID',N'CLASSIFY',0,0),(N'VALIDATE',N'MORE_INFORMATION_REQUIRED',N'CAPTURE',0,1),
(N'CLASSIFY',N'CLASSIFIED',N'ASSIGN',0,0),(N'ASSIGN',N'ASSIGNED',N'INVESTIGATE',0,0),(N'INVESTIGATE',N'EVIDENCE_COMPLETE',N'DECIDE',0,0),
(N'DECIDE',N'RETURNED_FOR_INVESTIGATION',N'INVESTIGATE',0,1),(N'DECIDE',N'APPROVED',N'RESOLVE',0,0),(N'RESOLVE',N'RESOLUTION_COMPLETED',N'CLOSE',0,0),(N'CLOSE',N'REOPENED',N'INVESTIGATE',1,1)
)v(FromStageCode,OutcomeCode,ToStageCode,IsReopen,IsReentry) WHERE ct.CaseTypeCode=N'SUPPLIER_NONCONFORMANCE')s
ON t.CaseTypeId=s.CaseTypeId AND t.FromStageCode=s.FromStageCode AND t.OutcomeCode=s.OutcomeCode AND t.ToStageCode=s.ToStageCode
WHEN MATCHED THEN UPDATE SET IsReopen=s.IsReopen,IsReentry=s.IsReentry,IsActive=1
WHEN NOT MATCHED THEN INSERT(CaseTypeId,FromStageCode,OutcomeCode,ToStageCode,IsReopen,IsReentry,IsActive) VALUES(s.CaseTypeId,s.FromStageCode,s.OutcomeCode,s.ToStageCode,s.IsReopen,s.IsReentry,1);
GO

IF OBJECT_ID(N'SNC.BusinessRule', N'U') IS NULL CREATE TABLE SNC.BusinessRule
(
 BusinessRuleId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_SNC_BusinessRule PRIMARY KEY, RuleCode nvarchar(50) NOT NULL, Name nvarchar(150) NOT NULL,
 Purpose nvarchar(500) NOT NULL, RuleType nvarchar(40) NOT NULL, AppliesToCaseType nvarchar(50) NULL, AppliesToStage nvarchar(50) NULL,
 Version nvarchar(30) NOT NULL, EffectiveFrom datetime2(0) NOT NULL, EffectiveTo datetime2(0) NULL, Priority int NOT NULL,
 Condition nvarchar(2000) NOT NULL, Result nvarchar(2000) NOT NULL, FailureMessage nvarchar(500) NULL, IsActive bit NOT NULL,
 CONSTRAINT UQ_SNC_BusinessRule UNIQUE(RuleCode,Version)
);
GO

IF OBJECT_ID(N'SNC.[Case]', N'U') IS NULL CREATE TABLE SNC.[Case]
(
 CaseId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_SNC_Case PRIMARY KEY, CaseNumber nvarchar(40) NOT NULL CONSTRAINT UQ_SNC_Case_Number UNIQUE,
 CaseTypeId int NOT NULL, Title nvarchar(200) NOT NULL, Description nvarchar(2000) NULL, Source nvarchar(100) NOT NULL,
 Status nvarchar(50) NOT NULL, CurrentStageCode nvarchar(50) NOT NULL, PreviousStageCode nvarchar(50) NULL,
 PriorityCode nvarchar(50) NOT NULL, SeverityCode nvarchar(50) NOT NULL, RiskCode nvarchar(50) NOT NULL, ConfidentialityCode nvarchar(50) NOT NULL,
 JurisdictionCode nvarchar(50) NULL, OwningTeam nvarchar(200) NULL, OwnerFQN nvarchar(300) NULL, RequesterPartyId int NULL, SubjectPartyId int NULL, ParentCaseId int NULL,
 OpenedDate datetime2(0) NOT NULL CONSTRAINT DF_SNC_Case_Opened DEFAULT(SYSUTCDATETIME()), TargetDate datetime2(0) NULL, ClosedDate datetime2(0) NULL,
 LastUpdatedDate datetime2(0) NOT NULL CONSTRAINT DF_SNC_Case_Updated DEFAULT(SYSUTCDATETIME()), StageEnteredDate datetime2(0) NOT NULL CONSTRAINT DF_SNC_Case_StageEntered DEFAULT(SYSUTCDATETIME()),
 SLAStatus nvarchar(40) NOT NULL CONSTRAINT DF_SNC_Case_SLA DEFAULT(N'OnTrack'), IsOnHold bit NOT NULL CONSTRAINT DF_SNC_Case_Hold DEFAULT(0), HoldReasonCode nvarchar(50) NULL,
 OutcomeCode nvarchar(50) NULL, ResolutionSummary nvarchar(2000) NULL, ConfigurationVersion nvarchar(30) NOT NULL, WorkflowInstanceId nvarchar(100) NULL, RowVersion rowversion NOT NULL,
 CONSTRAINT FK_SNC_Case_CaseType FOREIGN KEY(CaseTypeId) REFERENCES SNC.CaseType(CaseTypeId), CONSTRAINT FK_SNC_Case_Parent FOREIGN KEY(ParentCaseId) REFERENCES SNC.[Case](CaseId)
);
GO

IF OBJECT_ID(N'SNC.NonconformanceDetail', N'U') IS NULL CREATE TABLE SNC.NonconformanceDetail
(
 NonconformanceDetailId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_SNC_NonconformanceDetail PRIMARY KEY, CaseId int NOT NULL,
 SupplierId nvarchar(100) NOT NULL, SupplierName nvarchar(200) NOT NULL, PartNumber nvarchar(100) NULL, LotNumber nvarchar(100) NULL,
 QuantityAffected decimal(18,3) NULL, SpecificationReference nvarchar(300) NULL, ContainmentRequired bit NOT NULL, ContainmentSummary nvarchar(1000) NULL,
 CONSTRAINT UQ_SNC_NonconformanceDetail UNIQUE(CaseId), CONSTRAINT FK_SNC_NonconformanceDetail_Case FOREIGN KEY(CaseId) REFERENCES SNC.[Case](CaseId) ON DELETE CASCADE
);
GO

IF OBJECT_ID(N'SNC.CaseStageInstance', N'U') IS NULL CREATE TABLE SNC.CaseStageInstance
(
 CaseStageInstanceId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_SNC_CaseStageInstance PRIMARY KEY, CaseId int NOT NULL, StageCode nvarchar(50) NOT NULL,
 Iteration int NOT NULL, Status nvarchar(40) NOT NULL, StartedDate datetime2(0) NOT NULL, TargetDate datetime2(0) NULL, CompletedDate datetime2(0) NULL,
 CompletedByFQN nvarchar(300) NULL, OutcomeCode nvarchar(50) NULL, OutcomeReason nvarchar(1000) NULL, ChildWorkflowInstanceId nvarchar(100) NULL,
 SLAStatus nvarchar(40) NOT NULL, EscalationLevel int NOT NULL CONSTRAINT DF_SNC_Stage_Escalation DEFAULT(0), IsCurrent bit NOT NULL,
 StageStatus nvarchar(40) NULL, RequestedNextStageCode nvarchar(50) NULL, CompletionReason nvarchar(1000) NULL, RequiresEscalation bit NULL,
 EscalationReasonCode nvarchar(50) NULL, RequiresApproval bit NULL, DecisionId int NULL, ErrorCode nvarchar(100) NULL, ErrorMessage nvarchar(2000) NULL, ResultReference nvarchar(500) NULL,
 CONSTRAINT UQ_SNC_Stage_Iteration UNIQUE(CaseId,StageCode,Iteration), CONSTRAINT FK_SNC_Stage_Case FOREIGN KEY(CaseId) REFERENCES SNC.[Case](CaseId) ON DELETE CASCADE
);
GO
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'SNC.CaseStageInstance') AND name=N'UX_SNC_Stage_Current') CREATE UNIQUE INDEX UX_SNC_Stage_Current ON SNC.CaseStageInstance(CaseId) WHERE IsCurrent=1;
GO

IF OBJECT_ID(N'SNC.CaseParty', N'U') IS NULL CREATE TABLE SNC.CaseParty
(CasePartyId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_SNC_CaseParty PRIMARY KEY,CaseId int NOT NULL,PartyType nvarchar(50) NOT NULL,RoleCode nvarchar(50) NOT NULL,DisplayName nvarchar(200) NOT NULL,ExternalReference nvarchar(200) NULL,Email nvarchar(300) NULL,Phone nvarchar(50) NULL,Organisation nvarchar(200) NULL,SensitivityCode nvarchar(50) NULL,IsPrimary bit NOT NULL,CONSTRAINT FK_SNC_CaseParty_Case FOREIGN KEY(CaseId) REFERENCES SNC.[Case](CaseId) ON DELETE CASCADE);
GO
IF OBJECT_ID(N'SNC.EvidenceItem', N'U') IS NULL CREATE TABLE SNC.EvidenceItem
(EvidenceId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_SNC_Evidence PRIMARY KEY,CaseId int NOT NULL,StageInstanceId int NULL,EvidenceTypeCode nvarchar(50) NOT NULL,Title nvarchar(200) NOT NULL,Description nvarchar(1000) NULL,DocumentReference nvarchar(1000) NULL,SourceSystem nvarchar(100) NULL,SourceRecordId nvarchar(200) NULL,Version nvarchar(50) NULL,ReceivedDate datetime2(0) NOT NULL,SubmittedByFQN nvarchar(300) NOT NULL,VerificationStatus nvarchar(50) NOT NULL,VerifiedByFQN nvarchar(300) NULL,VerifiedDate datetime2(0) NULL,ConfidentialityCode nvarchar(50) NOT NULL,Hash nvarchar(200) NULL,IsRequired bit NOT NULL,IsCurrent bit NOT NULL,CONSTRAINT FK_SNC_Evidence_Case FOREIGN KEY(CaseId) REFERENCES SNC.[Case](CaseId) ON DELETE CASCADE,CONSTRAINT FK_SNC_Evidence_Stage FOREIGN KEY(StageInstanceId) REFERENCES SNC.CaseStageInstance(CaseStageInstanceId));
GO
IF OBJECT_ID(N'SNC.CaseTask', N'U') IS NULL CREATE TABLE SNC.CaseTask
(CaseTaskId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_SNC_CaseTask PRIMARY KEY,CaseId int NOT NULL,StageInstanceId int NULL,TaskTypeCode nvarchar(50) NOT NULL,AssignedRole nvarchar(100) NULL,AssignedGroup nvarchar(300) NULL,AssignedUserFQN nvarchar(300) NULL,Status nvarchar(40) NOT NULL,CreatedDate datetime2(0) NOT NULL,AcceptedDate datetime2(0) NULL,DueDate datetime2(0) NULL,CompletedDate datetime2(0) NULL,OutcomeCode nvarchar(50) NULL,EscalationLevel int NOT NULL,IsBlocking bit NOT NULL,WorkflowSerialNumber nvarchar(100) NULL,CONSTRAINT FK_SNC_CaseTask_Case FOREIGN KEY(CaseId) REFERENCES SNC.[Case](CaseId) ON DELETE CASCADE,CONSTRAINT FK_SNC_CaseTask_Stage FOREIGN KEY(StageInstanceId) REFERENCES SNC.CaseStageInstance(CaseStageInstanceId));
GO
IF OBJECT_ID(N'SNC.AIInteraction', N'U') IS NULL CREATE TABLE SNC.AIInteraction
(AIInteractionId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_SNC_AIInteraction PRIMARY KEY,CaseId int NOT NULL,StageInstanceId int NULL,PurposeCode nvarchar(50) NOT NULL,ProviderCode nvarchar(50) NOT NULL,ModelId nvarchar(100) NOT NULL,ModelVersion nvarchar(100) NULL,PromptTemplateCode nvarchar(100) NOT NULL,InputReference nvarchar(1000) NULL,OutputReference nvarchar(1000) NULL,ConfidenceScore decimal(5,4) NULL,CreatedDate datetime2(0) NOT NULL,ReviewedByFQN nvarchar(300) NULL,ReviewedDate datetime2(0) NULL,HumanDispositionCode nvarchar(50) NULL,ErrorCode nvarchar(100) NULL,ContainsSensitiveData bit NOT NULL,CorrelationId uniqueidentifier NOT NULL,CONSTRAINT FK_SNC_AI_Case FOREIGN KEY(CaseId) REFERENCES SNC.[Case](CaseId) ON DELETE CASCADE,CONSTRAINT FK_SNC_AI_Stage FOREIGN KEY(StageInstanceId) REFERENCES SNC.CaseStageInstance(CaseStageInstanceId));
GO
IF OBJECT_ID(N'SNC.Decision', N'U') IS NULL CREATE TABLE SNC.Decision
(DecisionId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_SNC_Decision PRIMARY KEY,CaseId int NOT NULL,StageInstanceId int NULL,DecisionTypeCode nvarchar(50) NOT NULL,RecommendationCode nvarchar(50) NULL,DispositionCode nvarchar(50) NOT NULL,Rationale nvarchar(2000) NOT NULL,DeciderFQN nvarchar(300) NOT NULL,DecisionDate datetime2(0) NOT NULL,ApprovalLevel nvarchar(50) NULL,AdverseFlag bit NOT NULL,EffectiveDate datetime2(0) NULL,ExpiryDate datetime2(0) NULL,SupersedesDecisionId int NULL,AIInteractionId int NULL,CONSTRAINT FK_SNC_Decision_Case FOREIGN KEY(CaseId) REFERENCES SNC.[Case](CaseId) ON DELETE CASCADE,CONSTRAINT FK_SNC_Decision_Stage FOREIGN KEY(StageInstanceId) REFERENCES SNC.CaseStageInstance(CaseStageInstanceId),CONSTRAINT FK_SNC_Decision_AI FOREIGN KEY(AIInteractionId) REFERENCES SNC.AIInteraction(AIInteractionId));
GO
IF OBJECT_ID(N'SNC.Communication', N'U') IS NULL CREATE TABLE SNC.Communication
(CommunicationId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_SNC_Communication PRIMARY KEY,CaseId int NOT NULL,StageInstanceId int NULL,Direction nvarchar(20) NOT NULL,ChannelCode nvarchar(50) NOT NULL,Recipient nvarchar(500) NOT NULL,Subject nvarchar(300) NULL,BodyReference nvarchar(1000) NULL,TemplateCode nvarchar(100) NULL,CreatedDate datetime2(0) NOT NULL,SentDate datetime2(0) NULL,DeliveryStatus nvarchar(50) NOT NULL,ExternalMessageId nvarchar(200) NULL,CONSTRAINT FK_SNC_Communication_Case FOREIGN KEY(CaseId) REFERENCES SNC.[Case](CaseId) ON DELETE CASCADE,CONSTRAINT FK_SNC_Communication_Stage FOREIGN KEY(StageInstanceId) REFERENCES SNC.CaseStageInstance(CaseStageInstanceId));
GO
IF OBJECT_ID(N'SNC.CaseComment', N'U') IS NULL CREATE TABLE SNC.CaseComment
(CommentId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_SNC_Comment PRIMARY KEY,CaseId int NOT NULL,StageInstanceId int NULL,CommentTypeCode nvarchar(50) NOT NULL,CommentText nvarchar(2000) NOT NULL,CreatedByFQN nvarchar(300) NOT NULL,CreatedDate datetime2(0) NOT NULL,VisibilityCode nvarchar(50) NOT NULL,IsPinned bit NOT NULL,CONSTRAINT FK_SNC_Comment_Case FOREIGN KEY(CaseId) REFERENCES SNC.[Case](CaseId) ON DELETE CASCADE,CONSTRAINT FK_SNC_Comment_Stage FOREIGN KEY(StageInstanceId) REFERENCES SNC.CaseStageInstance(CaseStageInstanceId));
GO
IF OBJECT_ID(N'SNC.CaseRelationship', N'U') IS NULL CREATE TABLE SNC.CaseRelationship
(CaseRelationshipId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_SNC_Relationship PRIMARY KEY,CaseId int NOT NULL,RelatedCaseId int NOT NULL,RelationshipTypeCode nvarchar(50) NOT NULL,CreatedByFQN nvarchar(300) NOT NULL,CreatedDate datetime2(0) NOT NULL,Reason nvarchar(1000) NULL,CONSTRAINT FK_SNC_Relationship_Case FOREIGN KEY(CaseId) REFERENCES SNC.[Case](CaseId),CONSTRAINT FK_SNC_Relationship_Related FOREIGN KEY(RelatedCaseId) REFERENCES SNC.[Case](CaseId));
GO
IF OBJECT_ID(N'SNC.CaseCommand', N'U') IS NULL CREATE TABLE SNC.CaseCommand
(CommandId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_SNC_Command PRIMARY KEY,CaseId int NOT NULL,CommandTypeCode nvarchar(50) NOT NULL,RequestedByFQN nvarchar(300) NOT NULL,RequestedDate datetime2(0) NOT NULL,Reason nvarchar(1000) NOT NULL,Status nvarchar(40) NOT NULL,ProcessedDate datetime2(0) NULL,ProcessingResult nvarchar(2000) NULL,CorrelationId uniqueidentifier NOT NULL,CONSTRAINT UQ_SNC_Command_Correlation UNIQUE(CorrelationId),CONSTRAINT FK_SNC_Command_Case FOREIGN KEY(CaseId) REFERENCES SNC.[Case](CaseId) ON DELETE CASCADE);
GO
IF OBJECT_ID(N'SNC.CorrectiveAction', N'U') IS NULL CREATE TABLE SNC.CorrectiveAction
(CorrectiveActionId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_SNC_CorrectiveAction PRIMARY KEY,CaseId int NOT NULL,StageInstanceId int NULL,ActionTypeCode nvarchar(50) NOT NULL,Description nvarchar(1000) NOT NULL,OwnerFQN nvarchar(300) NOT NULL,DueDate datetime2(0) NOT NULL,Status nvarchar(40) NOT NULL,VerificationResult nvarchar(1000) NULL,CompletedDate datetime2(0) NULL,CONSTRAINT FK_SNC_Action_Case FOREIGN KEY(CaseId) REFERENCES SNC.[Case](CaseId) ON DELETE CASCADE,CONSTRAINT FK_SNC_Action_Stage FOREIGN KEY(StageInstanceId) REFERENCES SNC.CaseStageInstance(CaseStageInstanceId));
GO
IF OBJECT_ID(N'SNC.AuditEvent', N'U') IS NULL CREATE TABLE SNC.AuditEvent
(AuditEventId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_SNC_Audit PRIMARY KEY,CaseId int NOT NULL,StageInstanceId int NULL,EventTypeCode nvarchar(50) NOT NULL,ObjectType nvarchar(100) NOT NULL,ObjectId nvarchar(100) NULL,ActorType nvarchar(50) NOT NULL,ActorFQN nvarchar(300) NULL,EventDate datetime2(0) NOT NULL,BeforeState nvarchar(max) NULL,AfterState nvarchar(max) NULL,Reason nvarchar(1000) NULL,CorrelationId uniqueidentifier NOT NULL,WorkflowInstanceId nvarchar(100) NULL,CONSTRAINT FK_SNC_Audit_Case FOREIGN KEY(CaseId) REFERENCES SNC.[Case](CaseId),CONSTRAINT FK_SNC_Audit_Stage FOREIGN KEY(StageInstanceId) REFERENCES SNC.CaseStageInstance(CaseStageInstanceId));
GO

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'SNC.NonconformanceDetail') AND name=N'IX_SNC_NonconformanceDetail_CaseId') CREATE INDEX IX_SNC_NonconformanceDetail_CaseId ON SNC.NonconformanceDetail(CaseId);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'SNC.CaseStageInstance') AND name=N'IX_SNC_CaseStageInstance_CaseId') CREATE INDEX IX_SNC_CaseStageInstance_CaseId ON SNC.CaseStageInstance(CaseId);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'SNC.CaseParty') AND name=N'IX_SNC_CaseParty_CaseId') CREATE INDEX IX_SNC_CaseParty_CaseId ON SNC.CaseParty(CaseId);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'SNC.EvidenceItem') AND name=N'IX_SNC_EvidenceItem_CaseId') CREATE INDEX IX_SNC_EvidenceItem_CaseId ON SNC.EvidenceItem(CaseId);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'SNC.CaseTask') AND name=N'IX_SNC_CaseTask_CaseId') CREATE INDEX IX_SNC_CaseTask_CaseId ON SNC.CaseTask(CaseId);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'SNC.AIInteraction') AND name=N'IX_SNC_AIInteraction_CaseId') CREATE INDEX IX_SNC_AIInteraction_CaseId ON SNC.AIInteraction(CaseId);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'SNC.Decision') AND name=N'IX_SNC_Decision_CaseId') CREATE INDEX IX_SNC_Decision_CaseId ON SNC.Decision(CaseId);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'SNC.Communication') AND name=N'IX_SNC_Communication_CaseId') CREATE INDEX IX_SNC_Communication_CaseId ON SNC.Communication(CaseId);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'SNC.CaseComment') AND name=N'IX_SNC_CaseComment_CaseId') CREATE INDEX IX_SNC_CaseComment_CaseId ON SNC.CaseComment(CaseId);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'SNC.CaseRelationship') AND name=N'IX_SNC_CaseRelationship_CaseId') CREATE INDEX IX_SNC_CaseRelationship_CaseId ON SNC.CaseRelationship(CaseId);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'SNC.CaseCommand') AND name=N'IX_SNC_CaseCommand_CaseId') CREATE INDEX IX_SNC_CaseCommand_CaseId ON SNC.CaseCommand(CaseId);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'SNC.CorrectiveAction') AND name=N'IX_SNC_CorrectiveAction_CaseId') CREATE INDEX IX_SNC_CorrectiveAction_CaseId ON SNC.CorrectiveAction(CaseId);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'SNC.AuditEvent') AND name=N'IX_SNC_AuditEvent_CaseId') CREATE INDEX IX_SNC_AuditEvent_CaseId ON SNC.AuditEvent(CaseId);
GO

CREATE OR ALTER VIEW SNC.CaseSummary AS
SELECT c.CaseId,c.CaseNumber,ct.Name CaseTypeName,c.Title,c.Status,c.CurrentStageCode,c.PriorityCode,c.SeverityCode,c.RiskCode,c.OwningTeam,c.OwnerFQN,c.OpenedDate,c.TargetDate,c.SLAStatus,c.IsOnHold,n.SupplierId,n.SupplierName,n.PartNumber,n.LotNumber,n.QuantityAffected
FROM SNC.[Case] c JOIN SNC.CaseType ct ON ct.CaseTypeId=c.CaseTypeId LEFT JOIN SNC.NonconformanceDetail n ON n.CaseId=c.CaseId;
GO

CREATE OR ALTER VIEW SNC.DashboardSummary AS
SELECT CAST(SUM(CASE WHEN Status NOT IN (N'Closed',N'Cancelled') THEN 1 ELSE 0 END) AS int) OpenCaseCount,
 CAST(SUM(CASE WHEN Status NOT IN (N'Closed',N'Cancelled') AND SLAStatus IN (N'AtRisk',N'Breached') THEN 1 ELSE 0 END) AS int) SLAAtRiskCount,
 CAST((SELECT COUNT(*) FROM SNC.CorrectiveAction WHERE Status NOT IN (N'Completed',N'Cancelled') AND DueDate<SYSUTCDATETIME()) AS int) OverdueActionCount,
 CAST(SUM(CASE WHEN Status NOT IN (N'Closed',N'Cancelled') AND RiskCode=N'High' THEN 1 ELSE 0 END) AS int) HighRiskCaseCount
FROM SNC.[Case];
GO
CREATE OR ALTER VIEW SNC.DashboardCasesByStage AS
SELECT CurrentStageCode StageLabel,CAST(COUNT_BIG(*) AS bigint) CaseCount FROM SNC.[Case]
WHERE Status NOT IN (N'Closed',N'Cancelled') GROUP BY CurrentStageCode
UNION ALL SELECT N'No open cases',CAST(0 AS bigint) WHERE NOT EXISTS (SELECT 1 FROM SNC.[Case] WHERE Status NOT IN (N'Closed',N'Cancelled'));
GO
CREATE OR ALTER VIEW SNC.DashboardIntakeTrend AS
SELECT CONVERT(char(7),OpenedDate,126) PeriodLabel,CAST(COUNT_BIG(*) AS bigint) CaseCount FROM SNC.[Case]
WHERE OpenedDate>=DATEADD(month,-11,DATEFROMPARTS(YEAR(SYSUTCDATETIME()),MONTH(SYSUTCDATETIME()),1)) GROUP BY CONVERT(char(7),OpenedDate,126)
UNION ALL SELECT N'No intake',CAST(0 AS bigint) WHERE NOT EXISTS (SELECT 1 FROM SNC.[Case] WHERE OpenedDate>=DATEADD(month,-11,DATEFROMPARTS(YEAR(SYSUTCDATETIME()),MONTH(SYSUTCDATETIME()),1)));
GO
CREATE OR ALTER VIEW SNC.DashboardUrgentWork AS
SELECT c.CaseId,c.CaseNumber,c.Title,c.CurrentStageCode,c.RiskCode,c.OwnerFQN,c.SLAStatus,c.TargetDate,n.SupplierName
FROM SNC.[Case] c LEFT JOIN SNC.NonconformanceDetail n ON n.CaseId=c.CaseId
WHERE c.Status NOT IN (N'Closed',N'Cancelled') AND (c.RiskCode=N'High' OR c.SLAStatus IN (N'AtRisk',N'Breached') OR c.TargetDate<SYSUTCDATETIME());
GO

CREATE OR ALTER VIEW SNC.ReportBacklogAging AS
SELECT CASE WHEN DATEDIFF(day,OpenedDate,SYSUTCDATETIME())<8 THEN N'0-7 days'
            WHEN DATEDIFF(day,OpenedDate,SYSUTCDATETIME())<15 THEN N'8-14 days'
            WHEN DATEDIFF(day,OpenedDate,SYSUTCDATETIME())<31 THEN N'15-30 days'
            WHEN DATEDIFF(day,OpenedDate,SYSUTCDATETIME())<61 THEN N'31-60 days' ELSE N'61+ days' END AgingBucket,
       CAST(COUNT_BIG(*) AS bigint) CaseCount
FROM SNC.[Case] WHERE Status NOT IN (N'Closed',N'Cancelled')
GROUP BY CASE WHEN DATEDIFF(day,OpenedDate,SYSUTCDATETIME())<8 THEN N'0-7 days'
              WHEN DATEDIFF(day,OpenedDate,SYSUTCDATETIME())<15 THEN N'8-14 days'
              WHEN DATEDIFF(day,OpenedDate,SYSUTCDATETIME())<31 THEN N'15-30 days'
              WHEN DATEDIFF(day,OpenedDate,SYSUTCDATETIME())<61 THEN N'31-60 days' ELSE N'61+ days' END
UNION ALL SELECT N'No backlog',CAST(0 AS bigint) WHERE NOT EXISTS (SELECT 1 FROM SNC.[Case] WHERE Status NOT IN (N'Closed',N'Cancelled'));
GO
CREATE OR ALTER VIEW SNC.ReportSLAStatus AS
SELECT COALESCE(NULLIF(SLAStatus,N''),N'Not set') SLAStatus,CAST(COUNT_BIG(*) AS bigint) CaseCount
FROM SNC.[Case] WHERE Status NOT IN (N'Closed',N'Cancelled') GROUP BY COALESCE(NULLIF(SLAStatus,N''),N'Not set')
UNION ALL SELECT N'No open cases',CAST(0 AS bigint) WHERE NOT EXISTS (SELECT 1 FROM SNC.[Case] WHERE Status NOT IN (N'Closed',N'Cancelled'));
GO
CREATE OR ALTER VIEW SNC.ReportClosureThroughput AS
SELECT CONVERT(char(7),ClosedDate,126) PeriodLabel,CAST(COUNT_BIG(*) AS bigint) ClosedCaseCount
FROM SNC.[Case] WHERE ClosedDate IS NOT NULL AND ClosedDate>=DATEADD(month,-11,DATEFROMPARTS(YEAR(SYSUTCDATETIME()),MONTH(SYSUTCDATETIME()),1))
GROUP BY CONVERT(char(7),ClosedDate,126)
UNION ALL SELECT N'No closures',CAST(0 AS bigint) WHERE NOT EXISTS (SELECT 1 FROM SNC.[Case] WHERE ClosedDate IS NOT NULL AND ClosedDate>=DATEADD(month,-11,DATEFROMPARTS(YEAR(SYSUTCDATETIME()),MONTH(SYSUTCDATETIME()),1)));
GO
CREATE OR ALTER VIEW SNC.ReportCorrectiveActionStatus AS
SELECT COALESCE(NULLIF(Status,N''),N'Not set') ActionStatus,CAST(COUNT_BIG(*) AS bigint) ActionCount
FROM SNC.CorrectiveAction GROUP BY COALESCE(NULLIF(Status,N''),N'Not set')
UNION ALL SELECT N'No corrective actions',CAST(0 AS bigint) WHERE NOT EXISTS (SELECT 1 FROM SNC.CorrectiveAction);
GO
CREATE OR ALTER VIEW SNC.ReportCaseOutcomes AS
SELECT COALESCE(NULLIF(OutcomeCode,N''),N'No outcome') OutcomeLabel,CAST(COUNT_BIG(*) AS bigint) CaseCount
FROM SNC.[Case] GROUP BY COALESCE(NULLIF(OutcomeCode,N''),N'No outcome')
UNION ALL SELECT N'No cases',CAST(0 AS bigint) WHERE NOT EXISTS (SELECT 1 FROM SNC.[Case]);
GO
CREATE OR ALTER VIEW SNC.ReportSupplierRecurrence AS
SELECT COALESCE(NULLIF(n.SupplierName,N''),N'Unknown supplier') SupplierName,CAST(COUNT_BIG(*) AS bigint) CaseCount
FROM SNC.[Case] c JOIN SNC.NonconformanceDetail n ON n.CaseId=c.CaseId
GROUP BY COALESCE(NULLIF(n.SupplierName,N''),N'Unknown supplier')
UNION ALL SELECT N'No suppliers',CAST(0 AS bigint) WHERE NOT EXISTS (SELECT 1 FROM SNC.[Case] c JOIN SNC.NonconformanceDetail n ON n.CaseId=c.CaseId);
GO

IF OBJECT_ID(N'SNC.CaseLifecycleState', N'U') IS NULL CREATE TABLE SNC.CaseLifecycleState
(
 CaseId int NOT NULL CONSTRAINT PK_SNC_CaseLifecycleState PRIMARY KEY,
 CaseStageInstanceId nvarchar(30) NOT NULL,
 StageWorkflowName nvarchar(200) NOT NULL,
 IsTerminal bit NOT NULL,
 ResolvedDate datetime2(0) NOT NULL,
 CONSTRAINT FK_SNC_CaseLifecycleState_Case FOREIGN KEY(CaseId) REFERENCES SNC.[Case](CaseId) ON DELETE CASCADE
);
GO

CREATE OR ALTER PROCEDURE SNC.ResolveCaseLifecycleStage
    @CaseId int
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;

    DECLARE @CaseTypeId int, @StageCode nvarchar(50), @CaseStatus nvarchar(50), @StageInstanceId int,
            @OutcomeCode nvarchar(50), @NextStageCode nvarchar(50), @StageWorkflowName nvarchar(200), @IsTerminal bit = 0;
    SELECT @CaseTypeId=CaseTypeId,@StageCode=CurrentStageCode,@CaseStatus=Status FROM SNC.[Case] WITH (UPDLOCK,HOLDLOCK) WHERE CaseId=@CaseId;
    IF @CaseTypeId IS NULL THROW 51100, 'Case was not found.', 1;
    IF @CaseStatus IN (N'Closed',N'Cancelled') SET @IsTerminal=1;

    IF @IsTerminal=0
    BEGIN
        SELECT @StageInstanceId=CaseStageInstanceId,@OutcomeCode=OutcomeCode FROM SNC.CaseStageInstance WITH (UPDLOCK,HOLDLOCK) WHERE CaseId=@CaseId AND IsCurrent=1;
        IF @StageInstanceId IS NULL
        BEGIN
            DECLARE @Iteration int=ISNULL((SELECT MAX(Iteration) FROM SNC.CaseStageInstance WHERE CaseId=@CaseId AND StageCode=@StageCode),0)+1;
            INSERT SNC.CaseStageInstance(CaseId,StageCode,Iteration,Status,StartedDate,TargetDate,SLAStatus,IsCurrent)
            SELECT @CaseId,@StageCode,@Iteration,N'Active',SYSUTCDATETIME(),DATEADD(hour,sp.ResolutionHours,SYSUTCDATETIME()),N'OnTrack',1
            FROM SNC.StageDefinition sd JOIN SNC.SLAProfile sp ON sp.SLAProfileId=sd.SLAProfileId WHERE sd.CaseTypeId=@CaseTypeId AND sd.StageCode=@StageCode AND sd.IsActive=1;
            SET @StageInstanceId=SCOPE_IDENTITY();
        END
        ELSE IF @OutcomeCode IS NOT NULL AND @OutcomeCode<>N'PENDING'
        BEGIN
            IF EXISTS(SELECT 1 FROM SNC.StageDefinition WHERE CaseTypeId=@CaseTypeId AND StageCode=@StageCode AND IsTerminal=1 AND @OutcomeCode=N'CLOSED')
            BEGIN
                UPDATE SNC.CaseStageInstance SET Status=N'Completed',CompletedDate=SYSUTCDATETIME(),IsCurrent=0 WHERE CaseStageInstanceId=@StageInstanceId;
                UPDATE SNC.[Case] SET PreviousStageCode=@StageCode,Status=N'Closed',ClosedDate=SYSUTCDATETIME(),LastUpdatedDate=SYSUTCDATETIME() WHERE CaseId=@CaseId;
                SET @IsTerminal=1; SET @StageInstanceId=NULL;
            END
            ELSE
            BEGIN
                SELECT TOP(1) @NextStageCode=ToStageCode FROM SNC.AllowedTransition WHERE CaseTypeId=@CaseTypeId AND FromStageCode=@StageCode AND OutcomeCode=@OutcomeCode AND IsActive=1 ORDER BY TransitionId;
                IF @NextStageCode IS NULL THROW 51101, 'No active allowed transition matches the current stage outcome.', 1;
                UPDATE SNC.CaseStageInstance SET Status=N'Completed',CompletedDate=SYSUTCDATETIME(),IsCurrent=0 WHERE CaseStageInstanceId=@StageInstanceId;
                UPDATE SNC.[Case] SET PreviousStageCode=@StageCode,CurrentStageCode=@NextStageCode,StageEnteredDate=SYSUTCDATETIME(),LastUpdatedDate=SYSUTCDATETIME(),Status=N'Active' WHERE CaseId=@CaseId;
                SET @StageCode=@NextStageCode;
                DECLARE @NextIteration int=ISNULL((SELECT MAX(Iteration) FROM SNC.CaseStageInstance WHERE CaseId=@CaseId AND StageCode=@StageCode),0)+1;
                INSERT SNC.CaseStageInstance(CaseId,StageCode,Iteration,Status,StartedDate,TargetDate,SLAStatus,IsCurrent)
                SELECT @CaseId,@StageCode,@NextIteration,N'Active',SYSUTCDATETIME(),DATEADD(hour,sp.ResolutionHours,SYSUTCDATETIME()),N'OnTrack',1
                FROM SNC.StageDefinition sd JOIN SNC.SLAProfile sp ON sp.SLAProfileId=sd.SLAProfileId WHERE sd.CaseTypeId=@CaseTypeId AND sd.StageCode=@StageCode AND sd.IsActive=1;
                SET @StageInstanceId=SCOPE_IDENTITY();
            END
        END
    END

    IF @IsTerminal=0 SELECT @StageWorkflowName=N'SNC.Supplier Nonconformance WFs\'+StageWorkflowName FROM SNC.StageDefinition WHERE CaseTypeId=@CaseTypeId AND StageCode=@StageCode AND IsActive=1;
    MERGE SNC.CaseLifecycleState AS target
    USING (SELECT @CaseId CaseId,CONVERT(nvarchar(30),ISNULL(@StageInstanceId,0)) CaseStageInstanceId,
                  ISNULL(@StageWorkflowName,N'') StageWorkflowName,@IsTerminal IsTerminal) AS source
    ON target.CaseId=source.CaseId
    WHEN MATCHED THEN UPDATE SET CaseStageInstanceId=source.CaseStageInstanceId,StageWorkflowName=source.StageWorkflowName,
        IsTerminal=source.IsTerminal,ResolvedDate=SYSUTCDATETIME()
    WHEN NOT MATCHED THEN INSERT(CaseId,CaseStageInstanceId,StageWorkflowName,IsTerminal,ResolvedDate)
        VALUES(source.CaseId,source.CaseStageInstanceId,source.StageWorkflowName,source.IsTerminal,SYSUTCDATETIME());
    COMMIT TRANSACTION;
    SELECT @StageInstanceId AS CaseStageInstanceId,@StageWorkflowName AS StageWorkflowName,@IsTerminal AS IsTerminal;
END;
GO

IF NOT EXISTS(SELECT 1 FROM SNC.[Case] WHERE CaseNumber=N'SNC-TEST-0001')
BEGIN
 DECLARE @caseTypeId int=(SELECT CaseTypeId FROM SNC.CaseType WHERE CaseTypeCode=N'SUPPLIER_NONCONFORMANCE');
 INSERT SNC.[Case](CaseNumber,CaseTypeId,Title,Description,Source,Status,CurrentStageCode,PriorityCode,SeverityCode,RiskCode,ConfidentialityCode,ConfigurationVersion)
 VALUES(N'SNC-TEST-0001',@caseTypeId,N'Out-of-tolerance supplier component',N'Dimensional inspection found a supplier component outside specification.',N'Incoming Inspection',N'Active',N'INVESTIGATE',N'High',N'Major',N'High',N'Internal',N'1');
 DECLARE @caseId int=SCOPE_IDENTITY();
 INSERT SNC.NonconformanceDetail(CaseId,SupplierId,SupplierName,PartNumber,LotNumber,QuantityAffected,SpecificationReference,ContainmentRequired,ContainmentSummary) VALUES(@caseId,N'SUP-100',N'Example Components Ltd',N'PART-42',N'LOT-2026-07',25,N'SPEC-42 REV C',1,N'Quarantine affected lot pending review.');
 INSERT SNC.CaseStageInstance(CaseId,StageCode,Iteration,Status,StartedDate,TargetDate,SLAStatus,IsCurrent) VALUES(@caseId,N'INVESTIGATE',1,N'Active',SYSUTCDATETIME(),DATEADD(hour,80,SYSUTCDATETIME()),N'OnTrack',1);
 INSERT SNC.EvidenceItem(CaseId,EvidenceTypeCode,Title,Description,DocumentReference,SourceSystem,SourceRecordId,Version,ReceivedDate,SubmittedByFQN,VerificationStatus,ConfidentialityCode,IsRequired,IsCurrent) VALUES(@caseId,N'OriginatingRecord',N'Incoming inspection record',N'Inspection measurement evidence',N'repository://quality/SNC-TEST-0001/inspection',N'QMS',N'INS-1001',N'1',SYSUTCDATETIME(),N'K2:TRIALS\Administrator',N'Pending',N'Internal',1,1);
 INSERT SNC.AuditEvent(CaseId,EventTypeCode,ObjectType,ObjectId,ActorType,ActorFQN,EventDate,Reason,CorrelationId) VALUES(@caseId,N'CASE_CREATED',N'Case',CONVERT(nvarchar(100),@caseId),N'User',N'K2:TRIALS\Administrator',SYSUTCDATETIME(),N'Deployment verification seed',NEWID());
END;
GO
