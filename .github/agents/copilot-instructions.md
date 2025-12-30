# timesheet-speck-kit Development Guidelines

Auto-generated from all feature plans. Last updated: 2025-12-30

## Active Technologies

### Backend
- .NET 10 (C#) - API and business logic
- Microsoft Agent Framework - Conversational AI orchestration
- Aspire 13 - Service orchestration and deployment
- Azure Cosmos DB (DocumentDB API) - Conversation storage
- Azure Blob Storage - Audit logs
- MongoDB (local dev) - Cosmos DB emulator
- Azurite (local dev) - Azure Storage emulator
- Application Insights - Monitoring and telemetry

### Frontend
- TypeScript - Type-safe development
- React 18+ - UI framework
- Vite - Build tool and dev server
- Zustand - State management
- shadcn/ui - UI component library
- AG-UI Protocol - Agent-to-UI communication
- Tailwind CSS - Styling

### External Integrations
- Azure AI Foundry - LLM hosting (GPT-4)
- Factorial HR API - Timesheet management

## Project Structure

```text
src/
├── AppHost/                      # Aspire 13 orchestration
├── HRAgent.Api/                  # Backend API
│   ├── Controllers/
│   ├── Services/                 # Agent orchestration, intent classification
│   ├── Models/
│   └── Program.cs
├── HRAgent.Contracts/            # Shared DTOs
├── HRAgent.Infrastructure/       # Persistence & telemetry
└── HRAgent.ServiceDefaults/      # Shared observability config

frontend/
├── src/
│   ├── components/
│   │   ├── chat/                 # Chat UI components
│   │   └── ui/                   # shadcn/ui components
│   ├── store/                    # Zustand stores
│   ├── services/                 # API clients
│   └── App.tsx
└── package.json

tests/
├── HRAgent.Api.Tests/            # Backend tests (xUnit)
│   ├── Unit/
│   ├── Integration/
│   └── Contract/
└── frontend.tests/               # Frontend tests (Vitest, Playwright)
```

## Commands

### Backend
```bash
dotnet restore                    # Restore packages
dotnet build                      # Build solution
dotnet test                       # Run tests
dotnet run --project src/AppHost  # Run with Aspire
```

### Frontend
```bash
npm install                       # Install dependencies
npm run dev                       # Start dev server
npm run build                     # Production build
npm test                          # Run tests
npm run lint                      # Run linter
```

### Docker
```bash
docker-compose -f docker-compose.dev.yml up -d    # Start local dependencies
```

## Code Style

### .NET (C#)
- Follow C# coding conventions
- Use nullable reference types
- Async/await for all I/O operations
- Dependency injection for all services
- StyleCop/Roslyn analyzers enforced
- XML documentation comments for public APIs

### TypeScript/React
- Strict TypeScript mode enabled
- Functional components with hooks
- ESLint + Prettier enforced
- Component structure: logic in hooks, presentation in components
- Zustand for state management (no Redux)

### Testing
- **TDD Required**: Write tests first (constitution principle)
- xUnit for .NET backend tests
- Vitest for React unit tests
- Playwright for E2E tests
- Minimum 80% code coverage

### AG-UI Protocol
- Use Server-Sent Events (SSE) for streaming
- Follow event types: message.*, tool_call.*, state.*, activity.*, error
- Implement proper error handling and reconnection logic

### Agent Framework Patterns
- Use `AgentThread` for conversation persistence
- Implement intent classification via triage agent
- Tool calls for external API integration (Factorial HR)
- Middleware for logging and telemetry

## Recent Changes

- 001-hr-chat-agent: Added .NET 10 (C#), TypeScript (React frontend)
- 001-hr-chat-agent: Added Microsoft Agent Framework, Aspire 13, AG-UI protocol
- 001-hr-chat-agent: Added Azure Cosmos DB, Blob Storage, Application Insights
- 001-hr-chat-agent: Added Vite, React, Zustand, shadcn/ui

<!-- MANUAL ADDITIONS START -->
<!-- MANUAL ADDITIONS END -->
