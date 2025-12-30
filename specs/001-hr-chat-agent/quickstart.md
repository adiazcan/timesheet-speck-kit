# Developer Quickstart Guide: HR Chat Agent

**Feature**: 001-hr-chat-agent  
**Date**: 2025-12-30  
**Version**: 1.0

## Overview

This guide will get you up and running with the HR Chat Agent application in under 30 minutes. You'll set up the local development environment, run the application, and interact with the chat interface.

---

## Prerequisites

### Required Software

- **.NET 10 SDK**: [Download](https://dotnet.microsoft.com/download/dotnet/10.0)
- **Node.js 20+ and npm**: [Download](https://nodejs.org/)
- **Docker Desktop**: [Download](https://www.docker.com/products/docker-desktop)
- **Git**: [Download](https://git-scm.com/)
- **Visual Studio Code** (recommended): [Download](https://code.visualstudio.com/)

### Recommended VS Code Extensions

```bash
code --install-extension ms-dotnettools.csdevkit
code --install-extension ms-azuretools.vscode-docker
code --install-extension dbaeumer.vscode-eslint
code --install-extension esbenp.prettier-vscode
code --install-extension bradlc.vscode-tailwindcss
```

---

## Step 1: Clone Repository

```bash
git clone https://github.com/your-org/timesheet-hr-agent.git
cd timesheet-hr-agent
git checkout 001-hr-chat-agent
```

---

## Step 2: Start Local Dependencies

### Start MongoDB and Azure Storage Emulator

```bash
# Start Docker containers for local development
docker-compose -f docker-compose.dev.yml up -d

# Verify containers are running
docker ps

# Expected output:
# - mongodb (port 27017)
# - azurite (ports 10000-10002)
```

**Troubleshooting**:
- If port 27017 is in use: Stop existing MongoDB or change port in `docker-compose.dev.yml`
- If Docker not running: Start Docker Desktop and retry

---

## Step 3: Configure Secrets

### Backend Configuration

```bash
cd src/HRAgent.Api

# Initialize user secrets
dotnet user-secrets init

# Set Azure AI Foundry endpoint (use mock for local dev)
dotnet user-secrets set "AzureAI:Endpoint" "http://localhost:8080"
dotnet user-secrets set "AzureAI:ApiKey" "mock-key-123"

# Set Factorial HR API credentials (use mock for local dev)
dotnet user-secrets set "FactorialHR:BaseUrl" "http://localhost:9090"
dotnet user-secrets set "FactorialHR:ApiKey" "sk_test_mock_123"

# Set connection strings for local dev
dotnet user-secrets set "ConnectionStrings:MongoDB" "mongodb://localhost:27017/hrapp"
dotnet user-secrets set "ConnectionStrings:AzureStorage" "UseDevelopmentStorage=true"
```

### Frontend Configuration

```bash
cd ../../frontend

# Create .env.local file
cat > .env.local << EOF
VITE_API_BASE_URL=http://localhost:5000
VITE_WS_URL=ws://localhost:5000
EOF
```

---

## Step 4: Install Dependencies

### Backend

```bash
cd src/AppHost
dotnet restore

cd ../HRAgent.Api
dotnet restore

cd ../HRAgent.Contracts
dotnet restore

cd ../HRAgent.Infrastructure
dotnet restore
```

### Frontend

```bash
cd ../../frontend
npm install
```

**Troubleshooting**:
- If `dotnet restore` fails: Ensure .NET 10 SDK is installed (`dotnet --version`)
- If `npm install` fails: Delete `node_modules` and `package-lock.json`, retry

---

## Step 5: Run the Application

### Option A: Using Aspire 13 (Recommended)

**Terminal 1: Start Aspire AppHost**
```bash
cd src/AppHost
dotnet run

# Expected output:
# ✅ Starting application...
# ✅ API running on http://localhost:5000
# ✅ Aspire Dashboard: http://localhost:15000
```

**Access Aspire Dashboard**: Open http://localhost:15000 in your browser
- View all services (API, MongoDB, Azurite)
- Monitor logs, traces, and metrics
- Health check status

**Terminal 2: Start Frontend Dev Server**
```bash
cd frontend
npm run dev

# Expected output:
# ✅ Vite dev server running on http://localhost:5173
```

### Option B: Manual Start (Without Aspire)

**Terminal 1: Start Backend API**
```bash
cd src/HRAgent.Api
dotnet run

# API will start on http://localhost:5000
```

**Terminal 2: Start Frontend**
```bash
cd frontend
npm run dev

# Frontend will start on http://localhost:5173
```

---

## Step 6: Access the Application

1. **Open Frontend**: Navigate to http://localhost:5173
2. **Mock Login**: Enter any employee ID (e.g., `emp_001`)
3. **Start Chatting**: Try these messages:
   - "I'm starting work now"
   - "Am I clocked in?"
   - "Clock me out"
   - "Show me my timesheet for today"

**Expected Behavior**:
- Agent responds with conversational messages
- Tool calls displayed: "⏳ Clocking you in..."
- Clock-in status updates in real-time
- Streaming text appears word-by-word

---

## Step 7: Verify Everything Works

### Health Check

```bash
curl http://localhost:5000/api/health

# Expected response:
# {
#   "status": "healthy",
#   "dependencies": {
#     "cosmosDb": "healthy",
#     "blobStorage": "healthy"
#   }
# }
```

### Test Conversation Endpoint

```bash
curl -X POST http://localhost:5000/api/conversation \
  -H "Content-Type: application/json" \
  -d '{
    "message": "Am I clocked in?",
    "sessionId": "test_session_001",
    "employeeId": "emp_001"
  }'

# Expected: SSE stream of AG-UI events
```

### Check Aspire Dashboard

1. Open http://localhost:15000
2. Navigate to **Traces** tab
3. Send a message in the chat interface
4. Verify trace appears showing:
   - HTTP request to `/api/conversation`
   - Intent classification
   - Factorial HR API call (if applicable)
   - Response generation

---

## Project Structure Reference

```
timesheet-hr-agent/
├── src/
│   ├── AppHost/                      # Aspire 13 orchestration
│   │   ├── Program.cs                # Service composition
│   │   └── appsettings.json
│   │
│   ├── HRAgent.Api/                  # Backend API
│   │   ├── Controllers/
│   │   │   ├── ConversationController.cs
│   │   │   └── HealthController.cs
│   │   ├── Services/
│   │   │   ├── AgentOrchestrator.cs
│   │   │   ├── IntentClassifier.cs
│   │   │   └── FactorialHRService.cs
│   │   ├── Models/
│   │   └── Program.cs
│   │
│   ├── HRAgent.Contracts/            # Shared contracts
│   │   ├── AgUI/
│   │   └── Factorial/
│   │
│   └── HRAgent.Infrastructure/       # Persistence & telemetry
│       ├── Persistence/
│       └── Telemetry/
│
├── frontend/                         # React SPA
│   ├── src/
│   │   ├── components/
│   │   │   ├── chat/
│   │   │   │   ├── ChatInterface.tsx
│   │   │   │   ├── MessageBubble.tsx
│   │   │   │   └── ChatInput.tsx
│   │   │   └── ui/              # shadcn/ui components
│   │   ├── store/
│   │   │   └── conversationStore.ts  # Zustand store
│   │   ├── services/
│   │   │   └── agUiClient.ts
│   │   └── App.tsx
│   ├── vite.config.ts
│   └── package.json
│
├── tests/
│   ├── HRAgent.Api.Tests/
│   └── frontend.tests/
│
├── docker-compose.dev.yml            # Local dev dependencies
└── README.md
```

---

## Development Workflows

### Running Tests

**Backend Tests**:
```bash
cd src/HRAgent.Api
dotnet test

# Run specific test
dotnet test --filter "FullyQualifiedName~IntentClassifierTests"

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

**Frontend Tests**:
```bash
cd frontend

# Unit tests (Vitest)
npm run test

# E2E tests (Playwright)
npm run test:e2e

# Watch mode
npm run test:watch
```

### Hot Reload

- **Backend**: Automatic hot reload with `dotnet run` (changes recompile automatically)
- **Frontend**: Vite HMR (Hot Module Replacement) updates instantly

### Debugging

**Backend (VS Code)**:
1. Open `src/HRAgent.Api` folder
2. Press `F5` to start debugging
3. Set breakpoints in `Controllers/` or `Services/`
4. Send request from frontend or curl

**Frontend (VS Code)**:
1. Install "Debugger for Chrome" extension
2. Press `F5`
3. Choose "Chrome: Launch"
4. Set breakpoints in `.tsx` files

### Database Inspection

**MongoDB**:
```bash
# Connect to MongoDB
docker exec -it mongodb mongosh

# Switch to hrapp database
use hrapp

# View conversations
db.conversations.find().limit(5)

# Count conversations
db.conversations.countDocuments()
```

**Azure Storage (Azurite)**:
```bash
# Install Azure Storage Explorer
# OR use Azure Storage Explorer VS Code extension
# Connect to: http://localhost:10000
```

---

## Mock Services (Development)

### Mock Azure AI Foundry LLM

**Use in-memory mock for local development** (no Azure account needed):

```csharp
// src/HRAgent.Api/Program.cs
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSingleton<ILLMService, MockLLMService>();
}
else
{
    builder.Services.AddSingleton<ILLMService, AzureAILLMService>();
}
```

**MockLLMService** returns pre-scripted responses:
- "I'm starting work" → "I'll clock you in right now..."
- "Am I clocked in?" → "Let me check your status..."

### Mock Factorial HR API

**WireMock server for Factorial HR** (automatically started by AppHost):

```csharp
// src/AppHost/Program.cs
var factorialMock = builder.AddContainer("factorial-mock", "wiremock/wiremock")
    .WithHttpEndpoint(port: 9090, name: "http")
    .WithBindMount("./mocks/factorial", "/home/wiremock");
```

**Mock responses** in `mocks/factorial/mappings/`:
- `clock-in.json`: Returns success response
- `clock-out.json`: Returns success response
- `current-status.json`: Returns mock timesheet data

---

## Troubleshooting

### Port Already in Use

```bash
# Find process using port
lsof -i :5000   # macOS/Linux
netstat -ano | findstr :5000  # Windows

# Kill process
kill -9 <PID>   # macOS/Linux
taskkill /PID <PID> /F  # Windows
```

### Docker Containers Not Starting

```bash
# Check Docker status
docker ps -a

# View container logs
docker logs mongodb
docker logs azurite

# Restart containers
docker-compose -f docker-compose.dev.yml restart

# Clean restart
docker-compose -f docker-compose.dev.yml down
docker-compose -f docker-compose.dev.yml up -d
```

### Backend API Not Responding

```bash
# Check API logs
cd src/HRAgent.Api
dotnet run --verbosity detailed

# Check AppHost logs
cd src/AppHost
dotnet run --verbosity detailed
```

### Frontend Build Errors

```bash
# Clear cache and reinstall
rm -rf node_modules package-lock.json
npm install

# Check TypeScript errors
npm run typecheck

# Check linting
npm run lint
```

### MongoDB Connection Errors

```bash
# Verify MongoDB is running
docker ps | grep mongodb

# Test connection
mongosh "mongodb://localhost:27017/hrapp"

# Reset database
docker exec -it mongodb mongosh
> use hrapp
> db.dropDatabase()
```

---

## Next Steps

### 1. Explore the Code

- **Start with**: `src/HRAgent.Api/Controllers/ConversationController.cs`
- **Understand**: AG-UI event streaming implementation
- **Follow**: Call flow from controller → orchestrator → agent → Factorial HR

### 2. Add a New Intent

**Example: Add "break" intent for recording breaks**

1. Update intent classifier in `Services/IntentClassifier.cs`
2. Add break handling in `Services/AgentOrchestrator.cs`
3. Create Factorial HR service method for breaks
4. Write tests in `Tests/IntentClassifierTests.cs`
5. Update frontend to display break status

### 3. Customize UI

- Modify `frontend/src/components/chat/` for chat UI changes
- Update `frontend/src/components/ui/` for shadcn components
- Customize theme in `frontend/tailwind.config.js`

### 4. Run E2E Tests

```bash
cd frontend
npm run test:e2e

# Specific test
npm run test:e2e -- clock-in-flow.spec.ts
```

### 5. Deploy to Azure (Coming Soon)

```bash
# Authenticate to Azure
az login

# Initialize Aspire deployment
azd init

# Deploy to Azure Container Apps
azd up
```

---

## Common Development Tasks

### Add a New Package

**Backend**:
```bash
cd src/HRAgent.Api
dotnet add package <PackageName>
```

**Frontend**:
```bash
cd frontend
npm install <package-name>
```

### Create a New Component

```bash
cd frontend
npx shadcn-ui add <component-name>

# Example: Add a dialog component
npx shadcn-ui add dialog
```

### Generate OpenAPI Client

```bash
# Generate TypeScript client from OpenAPI spec
npx @openapitools/openapi-generator-cli generate \
  -i specs/001-hr-chat-agent/contracts/openapi.yaml \
  -g typescript-fetch \
  -o frontend/src/generated/api
```

### View Application Insights Locally

```bash
# Aspire Dashboard already includes telemetry
# Open: http://localhost:15000

# View traces, metrics, logs for all services
```

---

## Resources

### Documentation
- [.NET Aspire Docs](https://learn.microsoft.com/en-us/dotnet/aspire/)
- [Microsoft Agent Framework](https://learn.microsoft.com/en-us/agent-framework/)
- [AG-UI Protocol](https://docs.ag-ui.com/)
- [Vite Documentation](https://vitejs.dev/)
- [Zustand State Management](https://github.com/pmndrs/zustand)
- [shadcn/ui Components](https://ui.shadcn.com/)

### Support
- **Team Chat**: #hr-agent-dev on Slack
- **Issues**: [GitHub Issues](https://github.com/your-org/timesheet-hr-agent/issues)
- **Docs**: [Confluence Space](https://your-org.atlassian.net/wiki/spaces/HRAPP)

---

## Quick Reference

### Useful Commands

```bash
# Backend
dotnet restore              # Restore packages
dotnet build                # Build solution
dotnet test                 # Run tests
dotnet run                  # Run application
dotnet watch run            # Run with hot reload

# Frontend
npm install                 # Install packages
npm run dev                 # Start dev server
npm run build               # Production build
npm run preview             # Preview production build
npm run test                # Run tests
npm run lint                # Run linter

# Docker
docker-compose up -d        # Start containers
docker-compose down         # Stop containers
docker-compose logs -f      # View logs
docker ps                   # List containers
docker exec -it <name> sh   # Shell into container

# Git
git checkout -b feature/... # New feature branch
git add .                   # Stage changes
git commit -m "..."         # Commit
git push origin feature/... # Push branch
```

### Default Ports

| Service | Port | URL |
|---------|------|-----|
| Frontend (Vite) | 5173 | http://localhost:5173 |
| Backend API | 5000 | http://localhost:5000 |
| Aspire Dashboard | 15000 | http://localhost:15000 |
| MongoDB | 27017 | mongodb://localhost:27017 |
| Azurite (Blob) | 10000 | http://localhost:10000 |
| Factorial Mock | 9090 | http://localhost:9090 |

---

## Conclusion

You're now ready to develop the HR Chat Agent! 🚀

**Happy coding!**

For questions or issues, reach out on the #hr-agent-dev Slack channel or create a GitHub issue.

---

**Quickstart Status**: ✅ Complete  
**Estimated Setup Time**: 20-30 minutes  
**Last Updated**: 2025-12-30
