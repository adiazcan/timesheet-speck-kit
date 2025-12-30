# Implementation Plan: HR Chat Agent for Timesheet Management

**Branch**: `001-hr-chat-agent` | **Date**: 2025-12-30 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/001-hr-chat-agent/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/commands/plan.md` for the execution workflow.

## Summary

A conversational AI agent for timesheet management enabling employees to submit, view, and query timesheet data through natural language conversation. Built with .NET 10, Microsoft Agent Framework orchestrated via Aspire 13, Vite React frontend using AG-UI protocol, Azure DocumentDB for conversation history, Azure Blob Storage for audit logs, and Azure AI Foundry for LLM hosting.

## Technical Context

**Language/Version**: .NET 10 (C#), TypeScript (React frontend)
**Primary Dependencies**: 
- Backend: Microsoft Agent Framework, Aspire 13 (orchestration), Azure SDK for .NET
- Frontend: Vite, React 18+, AG-UI protocol, Zustand (state management), shadcn/ui
- LLM: Azure AI Foundry SDK
**Storage**: 
- Production: Azure Cosmos DB (DocumentDB API) for conversation history, Azure Blob Storage for audit logs
- Development: MongoDB (local), Azure Storage Emulator (Azurite)
**Testing**: xUnit (.NET), Vitest (React), Playwright (E2E)
**Target Platform**: Azure Container Apps (production), local Docker/Aspire 13 (development)
**Project Type**: Web application (backend API + frontend SPA)
**Performance Goals**: 
- Timesheet submission: <200ms p95
- Status queries: <2s p95 (current day), <5s p95 (historical 30 days)
- LLM response generation: <3s p95
- Support 100 concurrent conversations without degradation
**Constraints**: 
- Must NOT use Semantic Kernel
- Must NOT use Microsoft Foundry Agent Service (use Microsoft Agent Framework)
- AG-UI protocol for frontend-backend communication
- Factorial HR API integration for timesheet operations
**Scale/Scope**: 
- Initial: 100-500 employees
- Design for: 10,000 employees, 1M+ timesheet entries/year
- 4 primary user stories (P1-P4), ~15 API endpoints

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### Initial Check (Pre-Phase 0) ⚠️ CONDITIONAL PASS

**I. Test-Driven Development (TDD)** ✅
- Tests will be written first for all business logic, API endpoints, and agent orchestration
- User story acceptance criteria map directly to test cases
- Backend: xUnit for unit/integration tests, contract tests for Factorial HR API
- Frontend: Vitest for component tests, Playwright for E2E conversation flows
- All tests reviewed and approved before implementation begins

**II. Code Quality Standards** ✅
- .NET: C# coding conventions, StyleCop/Roslyn analyzers, nullable reference types enabled
- TypeScript: ESLint + Prettier, strict TypeScript mode
- Pre-commit hooks enforce formatting and linting
- Code coverage target: ≥80% for new code
- PR reviews verify readability, DRY, SOLID, clear separation of concerns

**III. User Experience Consistency** ⚠️ REQUIRES ADAPTATION
- Constitution specifies Aspire 13 CLI as primary interface
- This feature uses conversational UI (chat interface) as primary interaction
- **Adaptation Justification**: Natural language conversation is the core value proposition for HR agent
- **Compliance Strategy**: 
  - Aspire 13 CLI used for development/deployment orchestration (not end-user feature)
  - Consistent conversational UX patterns: clear confirmations, error messages, status updates
  - AG-UI protocol ensures standardized frontend-backend communication
  - Consistent timestamp formats (ISO 8601), localized display for users

**IV. Performance Requirements** ✅
- Timesheet submission: <200ms p95 (meets <500ms CLI response requirement)
- Status queries: <2s p95 current, <5s p95 historical (within report generation target)
- 100 concurrent users supported (meets scalability requirement)
- Application Insights monitoring for performance tracking
- Indexed queries on DocumentDB, caching for frequently accessed data

**V. Aspire 13 CLI as Primary Interface** ⚠️ REQUIRES ADAPTATION
- **Adaptation Justification**: End-users interact via conversational chat UI, not CLI
- **Compliance Strategy**:
  - Aspire 13 orchestrates backend services (Agent Framework, databases, monitoring)
  - Development workflow uses Aspire 13 CLI: `dotnet run --project AppHost`
  - Deployment configuration via Aspire manifests
  - Internal admin/debugging operations can expose CLI commands if needed
  - User-facing feature is chat interface; infrastructure is Aspire 13

**Gate Status**: ⚠️ CONDITIONAL PASS
- Principles I, II, IV: Full compliance
- Principles III, V: Justified adaptations for conversational UI feature
- **Proceed to Phase 0** with understanding that constitution is optimized for CLI-first tools; adaptations documented and reasonable for chat-based feature

---

### Post-Design Check (Post-Phase 1) ✅ FULL PASS

**I. Test-Driven Development (TDD)** ✅ VALIDATED
- **Contracts Defined**: OpenAPI spec, AG-UI protocol spec, Factorial HR contract enable test-first development
- **Data Models Complete**: Clear entities for mocking and test data generation
- **Test Strategy Documented**: Unit, integration, contract, E2E test levels defined
- **Acceptance Criteria Mapped**: User stories (P1-P4) directly map to test scenarios
- **Implementation Ready**: Tests can be written before code based on contracts

**II. Code Quality Standards** ✅ VALIDATED
- **Architecture Clear**: Clean separation of concerns (Controllers, Services, Infrastructure)
- **SOLID Principles**: Interface-based design (IFactorialHRService, IAgentOrchestrator)
- **DRY**: Shared contracts library prevents duplication
- **Documentation**: XML comments required, OpenAPI docs generated, quickstart guide complete
- **Static Analysis**: Roslyn/StyleCop configured, ESLint/Prettier for frontend

**III. User Experience Consistency** ✅ VALIDATED (Adapted)
- **Consistent Patterns**: AG-UI protocol standardizes all agent interactions
- **Clear Feedback**: Tool call visualization, activity indicators, error messages
- **Predictable Responses**: Standardized event types (message.*, tool_call.*, state.*)
- **Progress Indicators**: Activity events for long-running operations
- **Error Handling**: Graceful degradation, retry strategies, user-friendly error messages
- **Adaptation Confirmed**: Conversational UX is appropriate for HR agent use case

**IV. Performance Requirements** ✅ VALIDATED
- **Response Times**: Architecture supports <200ms timesheet ops, <2s queries
- **Scalability**: Cosmos DB partitioning by employeeId enables 100+ concurrent users
- **Caching Strategy**: Redis for Factorial HR, MemoryCache for Cosmos DB queries
- **Monitoring**: Application Insights integration for p95 tracking
- **Resource Efficiency**: Serverless Cosmos DB, efficient SSE streaming
- **Performance Tests**: Benchmarks defined in test strategy

**V. Aspire 13 CLI as Primary Interface** ✅ VALIDATED (Adapted)
- **Infrastructure Use**: Aspire 13 orchestrates all backend services
- **Development Workflow**: `dotnet run --project AppHost` standardized
- **Service Discovery**: Automatic endpoint management via Aspire
- **Configuration Management**: Centralized via AppHost
- **Deployment**: Azure Container Apps via Aspire manifests
- **Monitoring**: Aspire Dashboard for observability
- **Adaptation Confirmed**: Aspire 13 used for infrastructure/tooling, chat UI for end-users

**Final Gate Status**: ✅ FULL PASS
- All principles validated against complete design
- Adaptations (Principles III, V) justified and architecturally sound
- Constitution compliance confirmed for conversational AI feature
- **Ready for Phase 2: Task Breakdown**

**Key Design Validations**:
1. ✅ Test-first development enabled by comprehensive contracts
2. ✅ High code quality enforced by tooling and architecture patterns
3. ✅ Consistent UX via AG-UI protocol standardization
4. ✅ Performance targets achievable with chosen architecture
5. ✅ Aspire 13 properly used for infrastructure orchestration

## Project Structure

### Documentation (this feature)

```text
specs/001-hr-chat-agent/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
│   ├── openapi.yaml     # REST API specification
│   ├── ag-ui-protocol.md # AG-UI message schemas
│   └── factorial-hr.md  # External API contract documentation
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
# Web application structure (backend + frontend)

src/
├── AppHost/                          # Aspire 13 orchestration host
│   ├── Program.cs                    # Service composition and configuration
│   ├── appsettings.json              # Aspire configuration
│   └── AppHost.csproj
│
├── HRAgent.Api/                      # Backend API (.NET 10)
│   ├── Program.cs                    # ASP.NET Core host
│   ├── Controllers/
│   │   ├── ConversationController.cs # AG-UI endpoint
│   │   └── HealthController.cs       # Health checks
│   ├── Services/
│   │   ├── AgentOrchestrator.cs      # Microsoft Agent Framework orchestration
│   │   ├── IntentClassifier.cs       # LLM-based intent detection
│   │   ├── FactorialHRService.cs     # Factorial HR API client
│   │   ├── ConversationStore.cs      # DocumentDB conversation persistence
│   │   └── AuditLogger.cs            # Blob Storage audit logging
│   ├── Models/
│   │   ├── ConversationMessage.cs
│   │   ├── TimesheetEntry.cs
│   │   └── AgentResponse.cs
│   ├── Configuration/
│   │   ├── AzureAIFoundryConfig.cs
│   │   ├── CosmosDbConfig.cs
│   │   └── BlobStorageConfig.cs
│   └── HRAgent.Api.csproj
│
├── HRAgent.Contracts/                # Shared contracts library
│   ├── AgUI/
│   │   ├── ConversationRequest.cs    # AG-UI protocol DTOs
│   │   └── ConversationResponse.cs
│   ├── Factorial/
│   │   ├── ClockInRequest.cs         # Factorial HR DTOs
│   │   └── TimesheetQuery.cs
│   └── HRAgent.Contracts.csproj
│
└── HRAgent.Infrastructure/           # Infrastructure concerns
    ├── Persistence/
    │   ├── CosmosDbClient.cs
    │   └── BlobStorageClient.cs
    ├── Telemetry/
    │   └── ApplicationInsightsConfig.cs
    └── HRAgent.Infrastructure.csproj

frontend/
├── src/
│   ├── main.tsx                      # Vite entry point
│   ├── App.tsx                       # Root component
│   ├── components/
│   │   ├── ChatInterface.tsx         # Main conversation UI
│   │   ├── MessageBubble.tsx         # Individual message display
│   │   ├── TimesheetCard.tsx         # Timesheet data visualization
│   │   └── ui/                       # shadcn/ui components
│   ├── services/
│   │   ├── agUiClient.ts             # AG-UI protocol client
│   │   └── websocketService.ts       # Real-time communication
│   ├── store/
│   │   ├── conversationStore.ts      # Zustand store for conversation state
│   │   └── authStore.ts              # User authentication state
│   ├── hooks/
│   │   ├── useConversation.ts        # Conversation management hook
│   │   └── useTimesheet.ts           # Timesheet operations hook
│   └── types/
│       ├── agui.ts                   # AG-UI TypeScript types
│       └── timesheet.ts              # Domain types
├── public/
├── index.html
├── vite.config.ts
├── tsconfig.json
└── package.json

tests/
├── HRAgent.Api.Tests/                # Backend tests
│   ├── Unit/
│   │   ├── IntentClassifierTests.cs
│   │   ├── AgentOrchestratorTests.cs
│   │   └── TimesheetValidationTests.cs
│   ├── Integration/
│   │   ├── ConversationFlowTests.cs
│   │   ├── FactorialHRIntegrationTests.cs
│   │   └── CosmosDbIntegrationTests.cs
│   └── Contract/
│       └── FactorialHRContractTests.cs
│
└── frontend.tests/                   # Frontend tests
    ├── unit/
    │   ├── ChatInterface.test.tsx
    │   └── conversationStore.test.ts
    └── e2e/
        ├── clock-in-flow.spec.ts
        └── timesheet-query.spec.ts

infra/
├── aspire/
│   ├── manifest.json                 # Aspire deployment manifest
│   └── secrets.json                  # Local dev secrets (gitignored)
├── azure/
│   ├── bicep/                        # Azure infrastructure as code
│   │   ├── main.bicep
│   │   ├── cosmosdb.bicep
│   │   └── container-app.bicep
│   └── aca/                          # Azure Container Apps config
└── docker-compose.dev.yml            # Local dev environment (MongoDB, Azurite)
```

**Structure Decision**: 
- **Web application** chosen (backend API + frontend SPA)
- Backend: .NET 10 solution with multiple projects (Api, Contracts, Infrastructure) following clean architecture
- Frontend: Vite React SPA with AG-UI protocol for backend communication
- Aspire 13 orchestration via AppHost project for local development and service composition
- Separation of concerns: API layer, business logic (services), persistence (infrastructure), shared contracts
- Tests mirror source structure with unit, integration, and contract test separation

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| Aspire 13 CLI not primary user interface | Feature is conversational AI agent; chat UI is the product | CLI interface would defeat purpose of natural language interaction; users need chat interface for conversational experience |
| Multiple storage systems (DocumentDB + Blob) | DocumentDB optimized for conversation state queries; Blob Storage for immutable audit logs | Single database would mix transaction/query patterns with write-once audit data; separate concerns improve performance and compliance |
| AG-UI protocol adds abstraction layer | Standardized protocol for agent-frontend communication; decouples implementation | Direct REST API would couple frontend to backend implementation details; AG-UI enables agent framework flexibility and future multi-modal interfaces |

**Justification Summary**: Adaptations are architecturally sound and essential for conversational AI feature. Constitution's CLI-first principle applies to infrastructure/tooling (Aspire 13 orchestration), while user-facing interface appropriately uses chat UI. Multi-storage approach follows best practices for CQRS and audit logging patterns.
