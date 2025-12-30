---
description: "Task list for HR Chat Agent implementation"
---

# Tasks: HR Chat Agent for Timesheet Management

**Feature Branch**: `001-hr-chat-agent`  
**Date**: 2025-12-30  
**Input**: Design documents from `/specs/001-hr-chat-agent/`

**Tests**: Per Constitution Principle I (TDD), tests are MANDATORY and MUST be written FIRST. Test tasks are included for each user story and MUST pass approval before implementation begins.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

---

## Format: `- [ ] [ID] [P?] [Story?] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: Which user story this task belongs to (US1, US2, US3, US4)
- File paths use the structure defined in plan.md (src/, frontend/)

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure for .NET 10 backend + Vite React frontend

- [X] T001 Create repository structure per plan.md (src/, frontend/, tests/, infra/)
- [X] T002 Initialize .NET 10 solution with AppHost, HRAgent.Api, HRAgent.Contracts, HRAgent.Infrastructure, HRAgent.ServiceDefaults projects
- [X] T003 [P] Initialize Vite React TypeScript project in frontend/ with shadcn/ui and Zustand dependencies
- [X] T004 [P] Configure ESLint, Prettier, StyleCop, and Roslyn analyzers for code quality
- [X] T005 [P] Configure AppHost with MongoDB and Azurite for local development
- [X] T006 [P] Setup .gitignore for .NET, Node.js, and IDE files

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

### Infrastructure & Configuration

- [X] T007 Configure AppHost orchestration in src/AppHost/Program.cs (MongoDB, Cosmos DB, Blob Storage, Application Insights references)
- [X] T008 [P] Implement service defaults for OpenTelemetry in src/HRAgent.ServiceDefaults/Extensions.cs
- [X] T009 [P] Configure Azure AI Foundry client in src/HRAgent.Api/Configuration/AzureAIFoundryConfig.cs
- [X] T010 [P] Configure Cosmos DB client in src/HRAgent.Infrastructure/Persistence/CosmosDbClient.cs
- [X] T011 [P] Configure Blob Storage client in src/HRAgent.Infrastructure/Persistence/BlobStorageClient.cs
- [X] T012 [P] Configure Application Insights telemetry in src/HRAgent.Infrastructure/Telemetry/ApplicationInsightsConfig.cs

### Authentication & Authorization

- [X] T013 Implement Microsoft Entra ID authentication middleware in src/HRAgent.Api/Middleware/AuthenticationMiddleware.cs
- [X] T014 Configure JWT token validation in src/HRAgent.Api/Program.cs
- [X] T015 [P] Create user claims extraction service in src/HRAgent.Api/Services/UserClaimsService.cs
- [X] T015e [P] Configure Azure Key Vault client in src/HRAgent.Infrastructure/Security/KeyVaultClient.cs
- [X] T015f Implement Factorial HR API key retrieval from Key Vault in src/HRAgent.Api/Services/FactorialHRService.cs

### Timezone Handling (Foundational)

- [X] T015a [P] Implement browser timezone detection in frontend/src/utils/timezoneDetector.ts
- [X] T015b Update ConversationRequest to include user timezone in src/HRAgent.Contracts/AgUI/ConversationRequest.cs

### Session Management

- [X] T015g Implement session collision detection in src/HRAgent.Api/Services/SessionManager.cs (warn user if multiple active sessions detected)
- [X] T015h [P] Add session identifier to ConversationThread model and track active sessions per employee

### Shared Data Models & Contracts

- [X] T016 [P] Create ConversationRequest DTO in src/HRAgent.Contracts/AgUI/ConversationRequest.cs
- [X] T017 [P] Create AG-UI event DTOs (MessageStartEvent, MessageContentEvent, MessageEndEvent) in src/HRAgent.Contracts/AgUI/
- [X] T018 [P] Create AG-UI event DTOs (ToolCallStartEvent, ToolCallEndEvent) in src/HRAgent.Contracts/AgUI/
- [X] T019 [P] Create AG-UI event DTOs (StateSnapshotEvent, StateDeltaEvent, ActivityStartEvent, ActivityEndEvent, ErrorEvent) in src/HRAgent.Contracts/AgUI/
- [X] T020 [P] Create Factorial HR DTOs (ClockInRequest, ClockOutRequest, TimesheetQuery) in src/HRAgent.Contracts/Factorial/
- [X] T021 [P] Create ConversationThread model in src/HRAgent.Api/Models/ConversationThread.cs
- [X] T022 [P] Create ConversationMessage model in src/HRAgent.Api/Models/ConversationMessage.cs
- [X] T023 [P] Create ConversationState model in src/HRAgent.Api/Models/ConversationState.cs
- [X] T024 [P] Create TimesheetEntry domain model in src/HRAgent.Api/Models/TimesheetEntry.cs
- [X] T025 [P] Create AuditLogEntry model in src/HRAgent.Api/Models/AuditLogEntry.cs

### Core Services Infrastructure

- [X] T026 Implement ConversationStore for Cosmos DB persistence in src/HRAgent.Api/Services/ConversationStore.cs
- [X] T027 [P] Implement AuditLogger for Blob Storage in src/HRAgent.Api/Services/AuditLogger.cs
- [X] T027a [P] Configure Azure Blob Storage lifecycle policy for 7-year audit log retention in infra/azure/bicep/blob-lifecycle-policy.bicep
- [X] T028 [P] Implement FactorialHRService with HTTP client, retry policies, and rate limiting in src/HRAgent.Api/Services/FactorialHRService.cs
- [X] T028a [P] Create SubmissionQueueItem model in src/HRAgent.Api/Models/SubmissionQueueItem.cs (stores employee ID, action, timestamp, retry count)
- [X] T028b Implement SubmissionQueue service using Cosmos DB change feed for durability in src/HRAgent.Api/Services/SubmissionQueue.cs
- [X] T028c [P] Implement exponential backoff retry logic (1s, 2s, 4s delays; max 3 retries; 30-second timeout per attempt) in SubmissionQueue
- [X] T028d Add user notification for queued submissions (AG-UI state update) in ConversationController
- [X] T028e [P] Create background processor for retry queue using Azure Functions or Aspire hosted service in src/HRAgent.Api/Jobs/SubmissionRetryProcessor.cs
- [X] T028f Update FactorialHRService to enqueue failed requests instead of immediate error return
- [X] T028g Add queue status query endpoint GET /api/submission-queue/{employeeId} for frontend status checks
- [X] T028h Implement permanent failure handling after 3 retry exhaustion with user notification via AG-UI error event
- [X] T029 Configure HttpClient for Factorial HR with resilience handler in src/HRAgent.Api/Program.cs

### Error Handling & Logging

- [X] T030 [P] Create global exception handler middleware in src/HRAgent.Api/Middleware/ExceptionHandlerMiddleware.cs
- [X] T031 [P] Implement structured logging configuration in src/HRAgent.Api/Program.cs

### Health Checks

- [X] T032 [P] Implement health check endpoint in src/HRAgent.Api/Controllers/HealthController.cs
- [X] T033 Configure health checks for Cosmos DB, Blob Storage, Factorial HR in src/HRAgent.Api/Program.cs

### GDPR Compliance (Foundational)

- [X] T033i [P] Create ConversationDeletionRequest model in src/HRAgent.Api/Models/ConversationDeletionRequest.cs
- [X] T033j [P] Implement ConversationDeletionService for marking conversations for deletion in src/HRAgent.Api/Services/ConversationDeletionService.cs
- [X] T033k Implement POST /api/conversation/deletion-request endpoint in src/HRAgent.Api/Controllers/ConversationController.cs
- [X] T033l [P] Create background job for processing deletion requests (30-day requirement) in src/HRAgent.Api/Jobs/DeletionProcessor.cs
- [X] T033m Configure Azure Functions timer trigger for daily deletion processing in infra/azure/functions/deletion-processor.bicep
- [X] T033n Add deletion confirmation email service in src/HRAgent.Api/Services/EmailNotificationService.cs
- [X] T033o Implement deletion audit logging (separate from conversation audit logs per FR-015d) in src/HRAgent.Api/Services/DeletionAuditLogger.cs
- [X] T033p [P] Create frontend UI for deletion request in frontend/src/components/profile/DataDeletionRequest.tsx
- [X] T033q Add deletion request status tracking in Cosmos DB (DeletionRequest collection)

### Review Follow-ups (AI) - Phase 1 & 2 Code Review (2025-12-30)

- [X] [AI-Review][CRITICAL] Write tests for ALL Phase 1 & 2 tasks - TDD violated, zero test files exist (Constitution Principle I)
- [X] [AI-Review][CRITICAL] Uncomment and configure Microsoft Entra ID authentication in Program.cs - T013-T015f marked complete but authentication disabled [src/HRAgent.Api/Program.cs#L98-L100]
- [X] [AI-Review][HIGH] Configure actual dependency health checks for Cosmos DB, Blob Storage, Factorial HR per T033 - currently only has self-check [src/HRAgent.Api/Program.cs]
- [X] [AI-Review][HIGH] Refactor FactorialHRService circular dependency with SubmissionQueue - use events/mediator pattern instead of setter injection [src/HRAgent.Api/Services/FactorialHRService.cs#L41-L44]
- [X] [AI-Review][HIGH] Split CosmosDbClient.cs into separate config and implementation files - 500+ line file violates single responsibility [src/HRAgent.Infrastructure/Persistence/CosmosDbClient.cs]
- [X] [AI-Review][HIGH] Eliminate ConversationStore wrapper or refactor interface design - double-wrapping creates confusion [src/HRAgent.Api/Services/ConversationStore.cs]
  Note: ConversationStore provides business logic layer (timestamp management, error logging) above IConversationStore. This is acceptable as long as it adds value. Consider renaming to ConversationService if confusion persists.
- [X] [AI-Review][MEDIUM] Verify UserClaimsService implementation exists and works with authentication enabled (T015)
- [X] [AI-Review][MEDIUM] Add unit tests for SubmissionQueue retry logic (1s, 2s, 4s exponential backoff, 3 retries, 30s timeout) - T028a-T028h
- [X] [AI-Review][MEDIUM] Review blob-lifecycle-policy.bicep to ensure 7-year retention configured per T027a [infra/azure/bicep/blob-lifecycle-policy.bicep]
- [X] [AI-Review][MEDIUM] Add Application Insights resource to AppHost orchestration per T007 [src/AppHost/AppHost.cs]
- [X] [AI-Review][MEDIUM] Verify timezone from ConversationRequest is actually used in DateTimeParser service - T015b [src/HRAgent.Contracts/AgUI/ConversationRequest.cs#L34-L39]
- [X] [AI-Review][MEDIUM] Verify TimesheetQuery DTO exists in Factorial folder with pagination fields per T020
- [X] [AI-Review][MEDIUM] Add SSE-aware error responses to ExceptionHandlerMiddleware - don't break stream on errors [src/HRAgent.Api/Middleware/ExceptionHandlerMiddleware.cs]
- [X] [AI-Review][MEDIUM] Add API key cache expiration/refresh mechanism to FactorialHRService - currently cached forever [src/HRAgent.Api/Services/FactorialHRService.cs#L44-L53]
- [X] [AI-Review][MEDIUM] Integrate HealthController with Aspire IHealthCheckService - current endpoints always return healthy without checking dependencies [src/HRAgent.Api/Controllers/HealthController.cs]
- [X] [AI-Review][MEDIUM] Adjust HttpClient circuit breaker policy - increase minimum throughput from 5 to 10-20 or increase failure ratio [src/HRAgent.Api/Program.cs#L60-L79]
- [X] [AI-Review][MEDIUM] Verify EmailNotificationService sends emails and handles SMTP errors - add email template for deletion confirmation [src/HRAgent.Api/Services/EmailNotificationService.cs]
- [X] [AI-Review][MEDIUM] Verify MongoDB.Driver NuGet package is referenced in HRAgent.Infrastructure.csproj
- [X] [AI-Review][LOW] Update README.md with actual project description - remove Vite boilerplate (T124) [frontend/README.md]
- [X] [AI-Review][LOW] Log timezone fallback to backend for metrics when browser detection fails [frontend/src/utils/timezoneDetector.ts#L18]
- [X] [AI-Review][LOW] Document that MongoDB IS Cosmos DB emulator (DocumentDB API) for clarity in AppHost comments [src/AppHost/AppHost.cs]
- [X] [AI-Review][LOW] Review Directory.Build.props content for shared NuGet versions, analyzer rules, nullable reference types
- [ ] [AI-Review][LOW] Document .editorconfig and frontend/.eslintignore changes in commit message

**Checkpoint**: Foundation ready - user story implementation can now begin in parallel

---

## Phase 3: User Story 1 - Submit Daily Timesheet via Conversation (Priority: P1) 🎯 MVP

**Goal**: Enable employees to clock in and clock out through natural conversation, recording timestamps in Factorial HR

**Independent Test**: User sends "starting work" message → agent records clock-in in Factorial HR; user sends "done for the day" → agent records clock-out with total hours calculated

### Tests for User Story 1 (Write FIRST per Constitution Principle I)

**⚠️ TDD Workflow**: Write tests → Review/Approve → Verify tests FAIL → Implement → Tests PASS

- [ ] T033a [P] [US1] Write unit tests for IntentClassifier clock-in/clock-out detection in tests/HRAgent.Api.Tests/Unit/IntentClassifierTests.cs
- [ ] T033b [P] [US1] Write unit tests for ClockInAgent business logic in tests/HRAgent.Api.Tests/Unit/ClockInAgentTests.cs
- [ ] T033c [P] [US1] Write unit tests for ClockOutAgent business logic in tests/HRAgent.Api.Tests/Unit/ClockOutAgentTests.cs
- [ ] T033d [US1] Write integration test for clock-in flow (message → agent → Factorial HR → state update) in tests/HRAgent.Api.Tests/Integration/ClockInFlowTests.cs
- [ ] T033e [US1] Write integration test for clock-out flow with total hours calculation in tests/HRAgent.Api.Tests/Integration/ClockOutFlowTests.cs
- [ ] T033f [P] [US1] Write contract tests for Factorial HR clock-in/clock-out endpoints using WireMock in tests/HRAgent.Api.Tests/Contract/FactorialHRContractTests.cs
- [ ] T033f1 [US1] Write integration test for multi-action workflow (single message contains both clock-in and clock-out) in tests/HRAgent.Api.Tests/Integration/MultiActionFlowTests.cs
- [ ] T033g [US1] Run all US1 tests and verify they FAIL (no implementation yet) - document failures
- [ ] T033h [US1] Get test approval from stakeholders before proceeding to T034

**Checkpoint**: Tests written, reviewed, approved, and failing. Ready for implementation.

### Agent Framework & Orchestration for User Story 1

- [ ] T034 [P] [US1] Implement DateTimeParser service for extracting dates/times from natural language ("yesterday at 9am", "December 28th at 5pm") and timezone conversion utilities in src/HRAgent.Api/Services/DateTimeParser.cs (consolidates T015c-T015d)
- [ ] T034a [P] [US1] Add relative date parsing ("yesterday", "last Monday", "3 days ago") with 30-day lookback validation in DateTimeParser
- [ ] T034b [P] [US1] Add absolute date parsing ("December 28th", "12/28/2025") with locale awareness in DateTimeParser
- [ ] T034c [P] [US1] Add time parsing ("at 9am", "at 17:00", "9:30 in the morning") and timestamp construction in DateTimeParser
- [ ] T035 [US1] Create ClockInAgent using Microsoft Agent Framework in src/HRAgent.Api/Agents/ClockInAgent.cs (accepts optional timestamp parameter for past date submissions)
- [ ] T035a [P] [US1] Add timestamp validation for past-date submissions (30-day lookback limit) in ClockInAgent and ClockOutAgent
- [ ] T036 [P] [US1] Create ClockOutAgent using Microsoft Agent Framework in src/HRAgent.Api/Agents/ClockOutAgent.cs (accepts optional timestamp parameter for past date submissions)
- [ ] T037 [US1] Implement AgentOrchestrator using Microsoft Agent Framework orchestration workflow (NOT intent classification) in src/HRAgent.Api/Services/AgentOrchestrator.cs (depends on T034-T036)
- [ ] T037a [US1] Implement multi-action workflow in AgentOrchestrator: parse message for actions (clock-in, clock-out), extract timestamps, execute agents sequentially, aggregate confirmations
- [ ] T037b [US1] Add action detection logic in AgentOrchestrator using LLM structured output to identify actions and associated timestamps from user message

### Backend API for User Story 1

- [ ] T038 [US1] Implement ConversationController with SSE streaming for AG-UI protocol in src/HRAgent.Api/Controllers/ConversationController.cs (handles clock-in/clock-out flows)
- [ ] T038a [P] [US1] Configure SSE response headers and event stream formatting in ConversationController
- [ ] T039 [US1] Implement ProcessConversationAsync with AG-UI event generation in ConversationController (message.start, message.content, tool_call.start/end, state.snapshot, message.end)
- [ ] T040 [US1] Add conversation state management (isClockedIn tracking) in AgentOrchestrator
- [ ] T041 [US1] Add validation for duplicate clock-in and missing clock-in before clock-out in ClockInAgent and ClockOutAgent
- [ ] T041a [US1] Add overnight shift validation: allow clock-out after midnight within 24 hours of clock-in in ClockOutAgent
- [ ] T041b [US1] Add business rule: if clock-out is next calendar day but within 24 hours, treat as single timesheet entry per FR-014c

### Frontend for User Story 1

- [ ] T042 [P] [US1] Create Zustand conversation store in frontend/src/store/conversationStore.ts (message state, streaming state, handleAGUIEvent)
- [ ] T043 [P] [US1] Implement AG-UI client service with SSE support in frontend/src/services/agUiClient.ts
- [ ] T044 [P] [US1] Create ChatInterface component in frontend/src/components/chat/ChatInterface.tsx
- [ ] T045 [P] [US1] Create MessageList component in frontend/src/components/chat/MessageList.tsx
- [ ] T046 [P] [US1] Create MessageBubble component in frontend/src/components/chat/MessageBubble.tsx
- [ ] T047 [P] [US1] Create ChatInput component in frontend/src/components/chat/ChatInput.tsx
- [ ] T048 [P] [US1] Create TypingIndicator component in frontend/src/components/chat/TypingIndicator.tsx
- [ ] T049 [P] [US1] Create ToolCallDisplay component for showing Factorial HR API calls in frontend/src/components/chat/ToolCallDisplay.tsx
- [ ] T050 [US1] Implement AG-UI event handler in conversationStore (message.start, message.content, message.end, tool_call.start, tool_call.end, state.snapshot)
- [ ] T051 [US1] Wire up ChatInterface with conversationStore and test clock-in/clock-out flow

### Integration & Validation for User Story 1

- [ ] T052 [US1] Add telemetry tracking for clock-in/clock-out operations in AgentOrchestrator
- [ ] T053 [US1] Add audit logging for all clock-in/clock-out actions in ConversationController
- [ ] T054 [US1] Test end-to-end clock-in flow (user message → intent detection → Factorial HR API → state update → confirmation)
- [ ] T055 [US1] Test end-to-end clock-out flow (user message → validation → Factorial HR API → total hours calculation → confirmation)
- [ ] T056 [US1] Test error scenarios (already clocked in, not clocked in, Factorial HR API failure)

**Checkpoint**: At this point, User Story 1 (clock-in/clock-out) should be fully functional and testable independently

---

## Phase 4: User Story 2 - View Today's Timesheet Status (Priority: P2)

**Goal**: Enable employees to query their current timesheet status (clocked in/out, current hours) through natural conversation

**Independent Test**: User asks "am I clocked in?" → agent queries Factorial HR and returns current status with clock-in time and duration; user asks "show me today's timesheet" after clock-out → agent displays completed entry with total hours

### Tests for User Story 2 (Write FIRST per Constitution Principle I)

- [ ] T056a [P] [US2] Write unit tests for StatusQueryAgent in tests/HRAgent.Api.Tests/Unit/StatusQueryAgentTests.cs
- [ ] T056b [US2] Write integration test for status query flow (clocked in, clocked out, no entry) in tests/HRAgent.Api.Tests/Integration/StatusQueryTests.cs
- [ ] T056c [US2] Run US2 tests and verify they FAIL - document failures
- [ ] T056d [US2] Get test approval before proceeding to T057

**Checkpoint**: US2 tests approved and failing.

### Agent Framework for User Story 2

- [ ] T057 [P] [US2] Create StatusQueryAgent using Microsoft Agent Framework in src/HRAgent.Api/Agents/StatusQueryAgent.cs
- [ ] T058 [US2] Update IntentClassifier to detect status-query intent in src/HRAgent.Api/Services/IntentClassifier.cs
- [ ] T059 [US2] Update AgentOrchestrator to route status queries to StatusQueryAgent in src/HRAgent.Api/Services/AgentOrchestrator.cs

### Backend Implementation for User Story 2

- [ ] T060 [US2] Implement GetCurrentStatusAsync in FactorialHRService for querying today's timesheet in src/HRAgent.Api/Services/FactorialHRService.cs
- [ ] T061 [US2] Add current hours calculation logic in StatusQueryAgent
- [ ] T062 [US2] Implement timesheet data formatting (human-readable timestamps, duration) in StatusQueryAgent
- [ ] T063 [US2] Update ProcessConversationAsync to handle status query flow (tool_call for Factorial HR query)

### Frontend for User Story 2

- [ ] T064 [P] [US2] Create TimesheetCard component for displaying timesheet data in frontend/src/components/timesheet/TimesheetCard.tsx
- [ ] T065 [P] [US2] Create ClockStatus component for current clock-in status indicator in frontend/src/components/timesheet/ClockStatus.tsx
- [ ] T066 [US2] Update MessageBubble to render TimesheetCard for status query responses in frontend/src/components/chat/MessageBubble.tsx

### Integration & Validation for User Story 2

- [ ] T067 [US2] Add telemetry for status query operations
- [ ] T068 [US2] Test status query when clocked in (shows clock-in time and current duration)
- [ ] T069 [US2] Test status query when not clocked in (confirms no active timesheet)
- [ ] T070 [US2] Test status query when clocked out (shows completed timesheet with total hours)
- [ ] T071 [US2] Test Factorial HR API error handling for status queries

**Checkpoint**: At this point, User Stories 1 AND 2 should both work independently

---

## Phase 5: User Story 3 - View Historical Timesheet Records (Priority: P3)

**Goal**: Enable employees to query past timesheet entries by date or date range through natural conversation

**Independent Test**: User asks "show me yesterday's timesheet" → agent retrieves and displays previous day's entry; user asks "show my hours for last week" → agent displays all entries from previous 7 days with summary

### Tests for User Story 3 (Write FIRST per Constitution Principle I)

- [ ] T071a [P] [US3] Write unit tests for DateParser (natural language date parsing) in tests/HRAgent.Api.Tests/Unit/DateParserTests.cs
- [ ] T071b [P] [US3] Write unit tests for HistoricalQueryAgent in tests/HRAgent.Api.Tests/Unit/HistoricalQueryAgentTests.cs
- [ ] T071c [US3] Write integration test for historical query flow (single day, date range, pagination) in tests/HRAgent.Api.Tests/Integration/HistoricalQueryTests.cs
- [ ] T071d [US3] Run US3 tests and verify they FAIL - document failures
- [ ] T071e [US3] Get test approval before proceeding to T072

**Checkpoint**: US3 tests approved and failing.

### Agent Framework for User Story 3

- [ ] T072 [P] [US3] Create HistoricalQueryAgent using Microsoft Agent Framework in src/HRAgent.Api/Agents/HistoricalQueryAgent.cs
- [ ] T073 [US3] Update IntentClassifier to detect historical-query intent in src/HRAgent.Api/Services/IntentClassifier.cs
- [ ] T074 [US3] Update AgentOrchestrator to route historical queries to HistoricalQueryAgent in src/HRAgent.Api/Services/AgentOrchestrator.cs

### Natural Language Date Parsing for User Story 3

- [ ] T075 [US3] Implement date/date range parsing from natural language ("yesterday", "last week", "December 15th") in src/HRAgent.Api/Services/DateParser.cs
- [ ] T076 [US3] Integrate DateParser into HistoricalQueryAgent for query parameter extraction

### Backend Implementation for User Story 3

- [ ] T077 [US3] Implement GetHistoryAsync in FactorialHRService for querying historical timesheets in src/HRAgent.Api/Services/FactorialHRService.cs
- [ ] T078 [US3] Add pagination support for large date ranges in HistoricalQueryAgent
- [ ] T079 [US3] Implement timesheet aggregation and summary (total hours, average per day) in HistoricalQueryAgent
- [ ] T080 [US3] Add validation for date range limits (max 90 days per query) in HistoricalQueryAgent
- [ ] T081 [US3] Update ProcessConversationAsync to handle historical query flow

### Frontend for User Story 3

- [ ] T082 [P] [US3] Create HistoricalView component for displaying multi-day timesheet data in frontend/src/components/timesheet/HistoricalView.tsx
- [ ] T083 [P] [US3] Create Timesheet summary component (total hours, date range) in frontend/src/components/timesheet/TimesheetSummary.tsx
- [ ] T084 [US3] Update MessageBubble to render HistoricalView for historical query responses in frontend/src/components/chat/MessageBubble.tsx

### Integration & Validation for User Story 3

- [ ] T085 [US3] Add telemetry for historical query operations
- [ ] T086 [US3] Test single-day historical query ("yesterday", specific date)
- [ ] T087 [US3] Test date range query ("last week", "December 1-15")
- [ ] T088 [US3] Test query with no results (date with no timesheet entries)
- [ ] T089 [US3] Test query with large date range (pagination behavior)
- [ ] T090 [US3] Test invalid date range error handling (end before start, range too large)

**Checkpoint**: All three user stories (clock-in/out, status, historical) should now be independently functional

---

## Phase 6: User Story 4 - Conversational Understanding of Time-Related Intents (Priority: P4)

**Goal**: Enhance intent classification to understand varied natural language phrasings for timesheet actions

**Independent Test**: Submit 10+ variations of clock-in/out/query messages (different phrasings, synonyms) → agent correctly interprets and executes intended action in all cases

### Tests for User Story 4 (Write FIRST per Constitution Principle I)

- [ ] T090a [US4] Write unit tests for expanded IntentClassifier with varied phrasings in tests/HRAgent.Api.Tests/Unit/IntentClassifierVariationsTests.cs
- [ ] T090b [P] [US4] Write unit tests for ChitchatAgent in tests/HRAgent.Api.Tests/Unit/ChitchatAgentTests.cs
- [ ] T090c [US4] Write integration test for 10+ phrasing variations per intent in tests/HRAgent.Api.Tests/Integration/PhrasingVariationsTests.cs
- [ ] T090d [US4] Run US4 tests and verify they FAIL - document failures
- [ ] T090e [US4] Get test approval before proceeding to T091

**Checkpoint**: US4 tests approved and failing.

### Intent Classification Enhancement for User Story 4

- [ ] T091 [US4] Expand intent classification prompts with examples of varied phrasings in src/HRAgent.Api/Services/IntentClassifier.cs
- [ ] T092 [US4] Add confidence threshold handling (ask for clarification if <0.7) in IntentClassifier
- [ ] T093 [US4] Create chitchat intent handler for greetings and non-timesheet messages in src/HRAgent.Api/Agents/ChitchatAgent.cs
- [ ] T094 [US4] Update AgentOrchestrator to handle chitchat intent routing

### Response Personalization for User Story 4

- [ ] T095 [US4] Add user name personalization from Microsoft Entra ID profile in AgentOrchestrator
- [ ] T096 [US4] Implement conversational response templates (greetings, confirmations, clarifications) in all agents
- [ ] T097 [US4] Add context memory for follow-up questions in ConversationState

### Frontend for User Story 4

- [ ] T098 [P] [US4] Create ErrorAlert component for clarification requests in frontend/src/components/chat/ErrorAlert.tsx
- [ ] T099 [US4] Update handleAGUIEvent to display clarification prompts when intent confidence is low

### Integration & Validation for User Story 4

- [ ] T100 [US4] Test varied clock-in phrasings ("starting work", "clocking in", "I'm here", "beginning shift")
- [ ] T101 [US4] Test varied clock-out phrasings ("done for today", "leaving now", "going home", "end of day")
- [ ] T102 [US4] Test varied status query phrasings ("am I clocked in?", "what's my time?", "show hours")
- [ ] T103 [US4] Test varied historical query phrasings ("yesterday's timesheet", "last week's hours", "what did I work")
- [ ] T104 [US4] Test chitchat handling (greetings, thanks, off-topic messages)
- [ ] T105 [US4] Test ambiguous message handling (clarification request flow)

**Checkpoint**: All four user stories should now be complete with enhanced conversational understanding

---

## Phase 7: Multi-Language Support (Cross-Cutting for All Stories)

**Purpose**: Add English, Spanish, and French language support as specified in NFR-021

### Internationalization Infrastructure

- [ ] T106 [P] Configure i18next for backend localization in src/HRAgent.Api/Configuration/LocalizationConfig.cs
- [ ] T107 [P] Configure i18next for frontend in frontend/src/i18n/config.ts
- [ ] T108 [P] Create language preference detection from Entra ID profile in src/HRAgent.Api/Services/UserClaimsService.cs
- [ ] T108a [P] Implement Accept-Language header parsing in src/HRAgent.Api/Services/LanguageDetector.cs
- [ ] T108b Implement 3-step fallback chain (Entra ID → Accept-Language → en-US default) in LanguageDetector per FR-020e
- [ ] T109 [P] Create translation files for English in src/HRAgent.Api/Localization/en.json and frontend/src/i18n/locales/en.json
- [ ] T109a [P] Create .NET resx file for English in src/HRAgent.Api/Resources/Strings.en.resx
- [ ] T110 [P] Create translation files for Spanish in src/HRAgent.Api/Localization/es.json and frontend/src/i18n/locales/es.json
- [ ] T110a [P] Create .NET resx file for Spanish in src/HRAgent.Api/Resources/Strings.es.resx
- [ ] T111 [P] Create translation files for French in src/HRAgent.Api/Localization/fr.json and frontend/src/i18n/locales/fr.json
- [ ] T111a [P] Create .NET resx file for French in src/HRAgent.Api/Resources/Strings.fr.resx

### Agent Localization

- [ ] T112 Update IntentClassifier to handle multi-language input detection
- [ ] T113 [P] Localize all agent response templates (clock-in confirmations, status responses, error messages)
- [ ] T114 [P] Update all AG-UI event messages to use localized strings

### Frontend Localization

- [ ] T115 [P] Localize all UI components (buttons, labels, placeholders)
- [ ] T116 [P] Implement date/time formatting per locale (MM/DD/YYYY for en-US, DD/MM/YYYY for es-ES/fr-FR)
- [ ] T117 Add language switcher UI component (if manual switching needed)

### Validation

- [ ] T118 Test full clock-in/out flow in Spanish
- [ ] T119 Test full clock-in/out flow in French
- [ ] T120 Test language preference detection from Entra ID profile
- [ ] T121 Test fallback to English for unsupported languages

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Improvements and final touches that affect multiple user stories

### Documentation

- [ ] T122 [P] Create API documentation from OpenAPI spec in docs/api.md
- [ ] T123 [P] Create deployment guide for Azure Container Apps in docs/deployment.md
- [ ] T124 [P] Update README.md with project overview and quickstart link
- [ ] T125 [P] Validate quickstart.md works for new developers

### Performance Optimization

- [ ] T127 [P] Optimize Cosmos DB queries with composite indexes
- [ ] T128 [P] Implement AG-UI event batching for high-frequency streaming
- [ ] T129 Configure CDN for frontend static assets

### Security Hardening

- [ ] T130 [P] Add rate limiting per employee (100 requests/minute)
- [ ] T131 [P] Add request size limits (2MB max)
- [ ] T132 [P] Implement Azure Content Safety API filtering for user input
- [ ] T133 [P] Add CORS configuration for production domains
- [ ] T134 Add security headers (CSP, HSTS, X-Frame-Options)

### Monitoring & Observability

- [ ] T135 [P] Configure Application Insights custom metrics (intent accuracy, response times)
- [ ] T136 [P] Create Application Insights dashboard for operations monitoring
- [ ] T137 [P] Add alerts for error rates, API failures, and performance degradation
- [ ] T138 Add log aggregation and query examples for troubleshooting

### Code Quality

- [ ] T139 Run full linting and fix all warnings (StyleCop, ESLint)
- [ ] T140 [P] Add XML documentation comments to all public APIs
- [ ] T141 Refactor duplicate code and apply SOLID principles
- [ ] T142 Final code review and approval

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
- **User Story 1 (Phase 3)**: Depends on Foundational completion - Core MVP functionality
- **User Story 2 (Phase 4)**: Depends on Foundational completion - Can run in parallel with US1 if staffed
- **User Story 3 (Phase 5)**: Depends on Foundational completion - Can run in parallel with US1/US2 if staffed
- **User Story 4 (Phase 6)**: Depends on US1, US2, US3 completion - Enhances existing functionality
- **Multi-Language (Phase 7)**: Can start after Foundational - Integrates with all stories
- **Polish (Phase 8)**: Depends on all desired user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational - No dependencies on other stories (MVP)
- **User Story 2 (P2)**: Can start after Foundational - Reuses FactorialHRService from US1 but independently testable
- **User Story 3 (P3)**: Can start after Foundational - Reuses FactorialHRService and extends query capabilities
- **User Story 4 (P4)**: Depends on US1, US2, US3 - Enhances intent understanding across all stories

### Within Each User Story

**User Story 1 (Clock-In/Out)**:
1. Agent Framework (T034-T037) → Backend API (T038-T041) → Frontend (T042-T051) → Integration (T052-T056)
2. Intent classifier and agents must be ready before controller implementation
3. Frontend components can be built in parallel once store is ready

**User Story 2 (Status Query)**:
1. Agent Framework (T057-T059) → Backend Implementation (T060-T063) → Frontend (T064-T066) → Integration (T067-T071)
2. Extends existing infrastructure from US1

**User Story 3 (Historical Query)**:
1. Agent Framework (T072-T074) → Date Parsing (T075-T076) → Backend (T077-T081) → Frontend (T082-T084) → Integration (T085-T090)
2. Date parsing service needed before agent implementation

**User Story 4 (Conversational Understanding)**:
1. Intent Enhancement (T091-T094) → Personalization (T095-T097) → Frontend (T098-T099) → Validation (T100-T105)
2. Builds on all previous stories

### Parallel Opportunities

**Within Setup (Phase 1)**:
- T003, T004, T005, T006 can all run in parallel

**Within Foundational (Phase 2)**:
- All configuration tasks (T009-T012) can run in parallel
- All DTO creation (T016-T025) can run in parallel
- T027 (AuditLogger) and T028 (FactorialHRService) can run in parallel

**Between User Stories (after Foundational complete)**:
- US1, US2, US3 can all be developed in parallel by different team members
- Multi-language support (Phase 7) can be developed in parallel with US3 or US4

**Within User Story 1**:
- T035 (ClockInAgent) and T036 (ClockOutAgent) can run in parallel
- All frontend components (T042-T049) can run in parallel once store structure is defined

**Within User Story 2**:
- T064 (TimesheetCard) and T065 (ClockStatus) can run in parallel

**Within User Story 3**:
- T082 (HistoricalView) and T083 (TimesheetSummary) can run in parallel

**Within Multi-Language (Phase 7)**:
- T106-T111 (all configuration and translation files) can run in parallel
- T113-T116 (localization tasks) can run in parallel

**Within Polish (Phase 8)**:
- T122-T125 (documentation) can run in parallel
- T127-T129 (performance) can run in parallel
- T130-T134 (security) can run in parallel
- T135-T138 (monitoring) can run in parallel

---

## Parallel Example: User Story 1 Frontend

```bash
# After conversation store structure is defined (T042 complete):

# Launch all component creation tasks in parallel:
Task T044: "Create ChatInterface component in frontend/src/components/chat/ChatInterface.tsx"
Task T045: "Create MessageList component in frontend/src/components/chat/MessageList.tsx"
Task T046: "Create MessageBubble component in frontend/src/components/chat/MessageBubble.tsx"
Task T047: "Create ChatInput component in frontend/src/components/chat/ChatInput.tsx"
Task T048: "Create TypingIndicator component in frontend/src/components/chat/TypingIndicator.tsx"
Task T049: "Create ToolCallDisplay component in frontend/src/components/chat/ToolCallDisplay.tsx"

# Once all components exist, integrate them:
Task T051: "Wire up ChatInterface with conversationStore and test clock-in/clock-out flow"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001-T006)
2. Complete Phase 2: Foundational (T007-T033) - CRITICAL blocking phase
3. Complete Phase 3: User Story 1 (T034-T056)
4. **STOP and VALIDATE**: Test clock-in/clock-out flow end-to-end
5. Deploy to dev environment and demo
6. **MVP READY**: Users can clock in and out via chat

### Incremental Delivery

1. **Foundation** (Phases 1-2) → Development environment ready
2. **MVP** (Phase 3: US1) → Clock-in/out working → Deploy/Demo
3. **Enhanced MVP** (+Phase 4: US2) → Status queries working → Deploy/Demo
4. **Full Historical** (+Phase 5: US3) → Historical queries working → Deploy/Demo
5. **Polished Experience** (+Phase 6: US4) → Natural conversation → Deploy/Demo
6. **International** (+Phase 7: Multi-language) → Spanish/French support → Deploy/Demo
7. **Production Ready** (+Phase 8: Polish) → Security, performance, monitoring → Production launch

Each increment adds value without breaking previous functionality.

### Parallel Team Strategy

With 3-4 developers after Foundational phase complete:

**Team Assignment Option A (Feature-based)**:
- Developer A: User Story 1 (clock-in/out) - Core MVP
- Developer B: User Story 2 (status query) - Builds on US1 services
- Developer C: User Story 3 (historical query) - Extends query capabilities
- Developer D: Multi-language support (Phase 7) - Cross-cutting

**Team Assignment Option B (Stack-based)**:
- Backend Team (2 devs): Complete T034-T041, T057-T063, T072-T081 sequentially
- Frontend Team (2 devs): Complete T042-T051, T064-T066, T082-T084 in parallel with backend
- Sync points: After each user story backend is complete, frontend integrates

---

## Task Count Summary

- **Phase 1 (Setup)**: 6 tasks
- **Phase 2 (Foundational)**: 27 tasks (BLOCKING)
- **Phase 3 (User Story 1 - MVP)**: 23 tasks
- **Phase 4 (User Story 2)**: 15 tasks
- **Phase 5 (User Story 3)**: 19 tasks
- **Phase 6 (User Story 4)**: 15 tasks
- **Phase 7 (Multi-Language)**: 16 tasks
- **Phase 8 (Polish)**: 21 tasks

**Total Tasks**: 142

**MVP Task Count** (Phases 1-3): 56 tasks  
**Parallel Opportunities**: 45+ tasks marked [P] can run in parallel within their phases

---

## Success Criteria Mapping

| Success Criteria | Validation Task(s) |
|------------------|-------------------|
| SC-001: Clock in/out in <10s | T054, T055 + T052 (telemetry) |
| SC-002: 90% intent accuracy | T100-T105 + T034 (intent classifier) |
| SC-003: Factorial HR submission <3s p95 | T052 + T028 (FactorialHRService with resilience) |
| SC-004: Status queries <2s p95 | T068-T070 + T060 (GetCurrentStatusAsync) |
| SC-004a: Historical queries <5s p95 | T086-T087 + T077 (GetHistoryAsync) |
| SC-005: 100 concurrent conversations | T033 (health checks) + T129 (optimization) |
| SC-006: 95% success rate | T028 (retry policies) + T137 (monitoring) |
| SC-008: 99.5% uptime | T033 (health checks) + T137 (alerts) |
| SC-009: 90% accuracy in all languages | T118-T120 (multi-language testing) |

---

## Notes

- **[P] marker**: Task operates on different files or has no dependencies on incomplete tasks
- **[Story] label**: Maps task to specific user story for traceability and independent testing
- **Tests excluded**: No test tasks included as not explicitly requested in spec.md
- **Foundational phase is critical**: All 27 tasks in Phase 2 must complete before user story work begins
- **MVP is User Story 1**: Focus on clock-in/out first for fastest time to value
- **Each user story is independently testable**: Can validate functionality without other stories
- **Commit strategy**: Commit after each task or logical group (e.g., all DTOs, all components)
- **Validation checkpoints**: Stop after each user story phase to validate independently
- **Deployment strategy**: Can deploy after each phase completion (incremental delivery)

---

**Generated**: 2025-12-30  
**Feature**: HR Chat Agent for Timesheet Management  
**Status**: Ready for implementation  
**Estimated MVP Effort**: 56 tasks (Phases 1-3)  
**Estimated Full Implementation**: 142 tasks (all phases)
