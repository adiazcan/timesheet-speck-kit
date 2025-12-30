# AG-UI Protocol Research Summary

## Executive Overview

**AG-UI (Agent-User Interaction Protocol)** is an open, lightweight, event-based protocol that standardizes how AI agents connect to user-facing applications. It serves as a bi-directional communication layer between agentic backends and frontend applications, enabling real-time, multimodal, interactive experiences.

---

## 1. What is AG-UI and Its Purpose?

### Core Definition
AG-UI is one of three prominent agentic protocols:
- **AG-UI**: Agent ↔ User Interaction (frontend-agent communication)
- **MCP**: Model Context Protocol - Agent ↔ Tools & Data (originated by Anthropic)
- **A2A**: Agent to Agent - Agent ↔ Agent (originated by Google)

### Purpose
AG-UI addresses the unique challenges of serving user-facing agents:
- Agents are **long-running** and stream intermediate work across multi-turn sessions
- Agents are **nondeterministic** and can control application UI dynamically
- Agents **mix structured + unstructured I/O** (text, voice, tool calls, state updates)
- Agents need **user-interactive composition** (sub-agents, recursive calls)

### Why Traditional REST/GraphQL APIs Don't Work
Agentic applications break the simple request/response model:
- Agents stream continuous updates, not single responses
- They maintain complex stateful interactions
- They require bidirectional real-time communication
- They produce both structured events and unstructured content

---

## 2. Message Structure and Protocol Specifications

### Base Event Properties
All AG-UI events share a common structure:

```typescript
interface BaseEvent {
  type: EventType        // Specific event type identifier
  timestamp?: number     // Optional creation timestamp
  rawEvent?: any         // Optional original event data if transformed
}
```

### Event Categories

#### **Lifecycle Events**
Monitor agent execution flow:
- `RunStarted`: Signals start of agent run
  ```typescript
  { threadId, runId, parentRunId?, input? }
  ```
- `RunFinished`: Successful completion
  ```typescript
  { threadId, runId, result? }
  ```
- `RunError`: Error during execution
  ```typescript
  { message, code? }
  ```
- `StepStarted`/`StepFinished`: Granular step tracking
  ```typescript
  { stepName }
  ```

#### **Text Message Events**
Handle streaming textual content (Start-Content-End pattern):
- `TextMessageStart`: Initialize new message
  ```typescript
  { messageId, role: "assistant" | "user" | "system" | "developer" | "tool" }
  ```
- `TextMessageContent`: Incremental content chunks
  ```typescript
  { messageId, delta: string }  // Append to previous chunks
  ```
- `TextMessageEnd`: Message completion
  ```typescript
  { messageId }
  ```
- `TextMessageChunk`: Convenience event (auto-expands to Start→Content→End)
  ```typescript
  { messageId?, role?, delta? }
  ```

#### **Tool Call Events**
Manage agent tool executions:
- `ToolCallStart`: Tool invocation begins
  ```typescript
  { toolCallId, toolCallName, parentMessageId? }
  ```
- `ToolCallArgs`: Stream tool arguments (often JSON fragments)
  ```typescript
  { toolCallId, delta }
  ```
- `ToolCallEnd`: Tool call specification complete
  ```typescript
  { toolCallId }
  ```
- `ToolCallResult`: Tool execution output
  ```typescript
  { messageId, toolCallId, content, role?: "tool" }
  ```
- `ToolCallChunk`: Convenience event for auto-expansion

#### **State Management Events**
Efficient snapshot-delta pattern:
- `StateSnapshot`: Complete state representation
  ```typescript
  { snapshot: object }
  ```
- `StateDelta`: Incremental updates via JSON Patch (RFC 6902)
  ```typescript
  { delta: JsonPatchOperation[] }
  ```
- `MessagesSnapshot`: Complete conversation history
  ```typescript
  { messages: Message[] }
  ```

#### **Activity Events**
Structured in-progress activity updates:
- `ActivitySnapshot`: Complete activity state
  ```typescript
  { messageId, activityType, content, replace?: boolean }
  ```
- `ActivityDelta`: Incremental activity updates (JSON Patch)
  ```typescript
  { messageId, activityType, patch: JsonPatchOperation[] }
  ```

#### **Special Events**
- `Raw`: Passthrough for external system events
  ```typescript
  { event: any, source?: string }
  ```
- `Custom`: Application-specific extensions
  ```typescript
  { name: string, value: any }
  ```

### Event Flow Patterns

1. **Start-Content-End Pattern**: Streaming content (messages, tool calls)
   - Start event initiates stream
   - Content events deliver data chunks
   - End event signals completion

2. **Snapshot-Delta Pattern**: State synchronization
   - Snapshot provides complete state
   - Delta events provide incremental updates

3. **Lifecycle Pattern**: Monitoring agent runs
   - Started events signal beginnings
   - Finished/Error events signal endings

---

## 3. Implementing AG-UI Client in React/TypeScript

### Core Architecture

```typescript
// Core agent execution interface
type RunAgent = () => Observable<BaseEvent>

class MyAgent extends AbstractAgent {
  run(input: RunAgentInput): RunAgent {
    const { threadId, runId } = input
    return () =>
      from([
        { type: EventType.RUN_STARTED, threadId, runId },
        {
          type: EventType.MESSAGES_SNAPSHOT,
          messages: [
            { id: "msg_1", role: "assistant", content: "Hello, world!" }
          ],
        },
        { type: EventType.RUN_FINISHED, threadId, runId },
      ])
  }
}
```

### Installation

```bash
# Core AG-UI packages
npm install @ag-ui/client @ag-ui/core

# For specific agent frameworks
npm install @ag-ui/mastra @mastra/core
# or @ag-ui/langgraph, @ag-ui/crewai, etc.
```

### Basic Client Implementation

```typescript
import { HttpAgent, EventType } from "@ag-ui/client"

// Create HTTP agent client
const agent = new HttpAgent({
  url: "https://your-agent-endpoint.com/agent",
  agentId: "unique-agent-id",
  threadId: "conversation-thread"
})

// Start agent and handle events
agent.runAgent({
  tools: [...],
  context: [...]
}).subscribe({
  next: (event) => {
    switch(event.type) {
      case EventType.TEXT_MESSAGE_CONTENT:
        // Update UI with new content
        updateChatUI(event.delta)
        break
      case EventType.TOOL_CALL_START:
        // Show tool execution indicator
        break
      // Handle other event types
    }
  },
  error: (error) => console.error("Agent error:", error),
  complete: () => console.log("Agent run complete")
})
```

### React Integration Pattern

```typescript
import { useEffect, useState } from 'react'
import { HttpAgent } from '@ag-ui/client'

function ChatComponent() {
  const [messages, setMessages] = useState<Message[]>([])
  const [currentMessage, setCurrentMessage] = useState('')
  
  const runAgent = async (userMessage: string) => {
    const agent = new HttpAgent({
      url: '/api/agent',
      threadId: 'user-conversation'
    })
    
    agent.runAgent({
      messages: [...messages, { role: 'user', content: userMessage }]
    }).subscribe({
      next: (event) => {
        if (event.type === 'TEXT_MESSAGE_CONTENT') {
          setCurrentMessage(prev => prev + event.delta)
        } else if (event.type === 'TEXT_MESSAGE_END') {
          setMessages(prev => [...prev, { 
            role: 'assistant', 
            content: currentMessage 
          }])
          setCurrentMessage('')
        }
      }
    })
  }
  
  return (
    <div>
      {messages.map(msg => <div key={msg.id}>{msg.content}</div>)}
      {currentMessage && <div>{currentMessage}</div>}
    </div>
  )
}
```

---

## 4. WebSocket vs HTTP Endpoints for Real-time Conversation

### Transport Options

AG-UI is **transport-agnostic** and supports multiple mechanisms:

#### **HTTP SSE (Server-Sent Events)** - Recommended for most use cases
- **Pros**:
  - Text-based, easy to read and debug
  - Wide browser compatibility
  - Simple to implement
  - Works through most proxies/firewalls
- **Cons**:
  - Unidirectional (server → client only)
  - Less efficient than binary protocols
- **Use Cases**: Standard chat applications, streaming responses

#### **HTTP Binary Protocol**
- **Pros**:
  - Highly performant
  - Space-efficient custom transport
  - Robust serialization for production
- **Cons**:
  - Harder to debug
  - More complex implementation
- **Use Cases**: High-traffic production environments

#### **WebSockets**
- **Pros**:
  - Bidirectional communication
  - Real-time, low-latency
  - Efficient for high-frequency updates
- **Cons**:
  - More complex to manage (connection state, reconnection)
  - May not work through all proxies
- **Use Cases**: Real-time collaborative features, sub-agents requiring feedback

### Standard HTTP Client

```typescript
const agent = new HttpAgent({
  url: "https://your-agent-endpoint.com/agent",
  agentId: "agent-id",
  threadId: "thread-id",
  // Automatically handles SSE or binary protocol
})
```

### Endpoint Contract

Backend endpoints must:
- Accept POST requests with `RunAgentInput` body
- Return stream of `BaseEvent` objects
- Support chosen transport (SSE or binary)

```typescript
interface RunAgentInput {
  threadId: string
  runId: string
  messages?: Message[]
  tools?: Tool[]
  context?: any
}
```

---

## 5. State Synchronization Between Frontend and Agent Framework

### Snapshot-Delta Pattern

AG-UI uses an efficient two-tier state synchronization approach:

#### **Initial State**: Snapshots
- Complete state representation at initialization
- Frontend replaces entire state (no merging)
- Used for:
  - Connection/reconnection
  - Recovery from inconsistencies
  - New conversation threads

```typescript
{
  type: "STATE_SNAPSHOT",
  snapshot: {
    userPreferences: { theme: "dark" },
    currentTask: { id: "task-1", status: "in-progress" },
    applicationState: { ... }
  }
}
```

#### **Ongoing Updates**: Deltas
- Incremental changes via JSON Patch (RFC 6902)
- Bandwidth-efficient (only changed data)
- Applied sequentially to maintain consistency

```typescript
{
  type: "STATE_DELTA",
  delta: [
    { op: "replace", path: "/currentTask/status", value: "completed" },
    { op: "add", path: "/tasks/-", value: { id: "task-2", status: "new" } }
  ]
}
```

### Implementation Strategy

```typescript
class StateManager {
  private state: any = {}
  
  handleEvent(event: BaseEvent) {
    switch (event.type) {
      case EventType.STATE_SNAPSHOT:
        // Replace entire state
        this.state = event.snapshot
        break
        
      case EventType.STATE_DELTA:
        // Apply JSON Patch operations
        this.state = applyPatch(this.state, event.delta)
        break
    }
    
    // Notify state subscribers
    this.notifySubscribers(this.state)
  }
}
```

### Conversation History

Separate event for message synchronization:

```typescript
{
  type: "MESSAGES_SNAPSHOT",
  messages: [
    { id: "msg-1", role: "user", content: "Hello" },
    { id: "msg-2", role: "assistant", content: "Hi there!" }
  ]
}
```

### Conflict Resolution
- If frontend detects state divergence, request fresh `StateSnapshot`
- Agent can send new snapshot at any time to reset state
- Critical for:
  - Network interruptions
  - Multi-device synchronization
  - Agent state corrections

---

## 6. Best Practices for Rendering Agent Responses in Chat UI

### Streaming Message Display

```typescript
function StreamingMessage({ messageId }: { messageId: string }) {
  const [content, setContent] = useState('')
  const [isComplete, setIsComplete] = useState(false)
  
  useEffect(() => {
    const subscription = agent.events$.subscribe(event => {
      if (event.messageId === messageId) {
        switch (event.type) {
          case 'TEXT_MESSAGE_CONTENT':
            setContent(prev => prev + event.delta)
            break
          case 'TEXT_MESSAGE_END':
            setIsComplete(true)
            break
        }
      }
    })
    
    return () => subscription.unsubscribe()
  }, [messageId])
  
  return (
    <div className={isComplete ? 'message-complete' : 'message-streaming'}>
      {content}
      {!isComplete && <LoadingIndicator />}
    </div>
  )
}
```

### Tool Call Visualization

```typescript
function ToolCallDisplay({ toolCallId }: { toolCallId: string }) {
  const [toolName, setToolName] = useState('')
  const [args, setArgs] = useState('')
  const [result, setResult] = useState(null)
  
  useEffect(() => {
    const subscription = agent.events$.subscribe(event => {
      if (event.toolCallId === toolCallId) {
        switch (event.type) {
          case 'TOOL_CALL_START':
            setToolName(event.toolCallName)
            break
          case 'TOOL_CALL_ARGS':
            setArgs(prev => prev + event.delta)
            break
          case 'TOOL_CALL_RESULT':
            setResult(event.content)
            break
        }
      }
    })
    
    return () => subscription.unsubscribe()
  }, [toolCallId])
  
  return (
    <div className="tool-call">
      <div className="tool-name">🔧 {toolName}</div>
      <pre className="tool-args">{args}</pre>
      {result && <div className="tool-result">{result}</div>}
    </div>
  )
}
```

### Message Grouping by Role

```typescript
function ChatMessages({ messages }: { messages: Message[] }) {
  return (
    <div className="chat-container">
      {messages.map(msg => (
        <div 
          key={msg.id} 
          className={`message message-${msg.role}`}
        >
          {msg.role === 'assistant' && <BotAvatar />}
          {msg.role === 'user' && <UserAvatar />}
          <div className="message-content">
            {msg.content}
          </div>
        </div>
      ))}
    </div>
  )
}
```

### Progressive Loading States

```typescript
function AgentThinking() {
  const [step, setStep] = useState('')
  
  useEffect(() => {
    const subscription = agent.events$.subscribe(event => {
      if (event.type === 'STEP_STARTED') {
        setStep(event.stepName)
      }
    })
    
    return () => subscription.unsubscribe()
  }, [])
  
  return (
    <div className="thinking-indicator">
      <Spinner />
      <span>Agent is {step || 'thinking'}...</span>
    </div>
  )
}
```

### Activity/Progress Display

```typescript
function ActivityDisplay({ activityId }: { activityId: string }) {
  const [activity, setActivity] = useState<any>({})
  
  useEffect(() => {
    const subscription = agent.events$.subscribe(event => {
      if (event.messageId === activityId) {
        switch (event.type) {
          case 'ACTIVITY_SNAPSHOT':
            setActivity(event.content)
            break
          case 'ACTIVITY_DELTA':
            setActivity(prev => applyPatch(prev, event.patch))
            break
        }
      }
    })
    
    return () => subscription.unsubscribe()
  }, [activityId])
  
  // Render based on activityType
  if (activity.activityType === 'SEARCH') {
    return <SearchActivity data={activity} />
  } else if (activity.activityType === 'PLAN') {
    return <PlanActivity data={activity} />
  }
  
  return null
}
```

### Error Handling

```typescript
function ChatWithErrorHandling() {
  const [error, setError] = useState<string | null>(null)
  
  useEffect(() => {
    const subscription = agent.events$.subscribe({
      next: (event) => {
        if (event.type === 'RUN_ERROR') {
          setError(event.message)
        }
      },
      error: (err) => {
        setError('Connection error: ' + err.message)
      }
    })
    
    return () => subscription.unsubscribe()
  }, [])
  
  if (error) {
    return (
      <div className="error-banner">
        ⚠️ {error}
        <button onClick={() => setError(null)}>Dismiss</button>
      </div>
    )
  }
  
  return <Chat />
}
```

---

## 7. Integration Patterns with Zustand for State Management

### Zustand Store for AG-UI Events

```typescript
import create from 'zustand'
import { BaseEvent } from '@ag-ui/client'

interface ChatStore {
  messages: Message[]
  agentState: any
  activities: Map<string, any>
  currentStreamingMessage: string
  isAgentRunning: boolean
  
  // Actions
  handleEvent: (event: BaseEvent) => void
  addMessage: (message: Message) => void
  updateAgentState: (state: any) => void
  setAgentRunning: (running: boolean) => void
}

export const useChatStore = create<ChatStore>((set, get) => ({
  messages: [],
  agentState: {},
  activities: new Map(),
  currentStreamingMessage: '',
  isAgentRunning: false,
  
  handleEvent: (event: BaseEvent) => {
    const { type } = event
    
    switch (type) {
      case 'RUN_STARTED':
        set({ isAgentRunning: true })
        break
        
      case 'RUN_FINISHED':
        set({ isAgentRunning: false })
        break
        
      case 'TEXT_MESSAGE_START':
        set({ currentStreamingMessage: '' })
        break
        
      case 'TEXT_MESSAGE_CONTENT':
        set(state => ({
          currentStreamingMessage: state.currentStreamingMessage + event.delta
        }))
        break
        
      case 'TEXT_MESSAGE_END':
        const { messages, currentStreamingMessage } = get()
        set({
          messages: [
            ...messages,
            {
              id: event.messageId,
              role: 'assistant',
              content: currentStreamingMessage
            }
          ],
          currentStreamingMessage: ''
        })
        break
        
      case 'STATE_SNAPSHOT':
        set({ agentState: event.snapshot })
        break
        
      case 'STATE_DELTA':
        set(state => ({
          agentState: applyPatch(state.agentState, event.delta)
        }))
        break
        
      case 'ACTIVITY_SNAPSHOT':
        set(state => {
          const activities = new Map(state.activities)
          activities.set(event.messageId, event.content)
          return { activities }
        })
        break
        
      case 'ACTIVITY_DELTA':
        set(state => {
          const activities = new Map(state.activities)
          const current = activities.get(event.messageId) || {}
          activities.set(event.messageId, applyPatch(current, event.patch))
          return { activities }
        })
        break
    }
  },
  
  addMessage: (message) => 
    set(state => ({ messages: [...state.messages, message] })),
    
  updateAgentState: (agentState) => 
    set({ agentState }),
    
  setAgentRunning: (isAgentRunning) => 
    set({ isAgentRunning })
}))
```

### React Component Integration

```typescript
function ChatInterface() {
  const { 
    messages, 
    currentStreamingMessage, 
    isAgentRunning,
    handleEvent,
    addMessage 
  } = useChatStore()
  
  const agent = useRef<HttpAgent>()
  
  useEffect(() => {
    agent.current = new HttpAgent({
      url: '/api/agent',
      threadId: 'conversation-1'
    })
    
    // Subscribe to all events
    const subscription = agent.current.events$.subscribe({
      next: handleEvent,
      error: (err) => console.error('Agent error:', err)
    })
    
    return () => subscription.unsubscribe()
  }, [handleEvent])
  
  const sendMessage = async (text: string) => {
    // Add user message immediately
    addMessage({
      id: generateId(),
      role: 'user',
      content: text
    })
    
    // Run agent
    await agent.current?.runAgent({
      messages: [...messages, { role: 'user', content: text }]
    })
  }
  
  return (
    <div className="chat">
      <MessageList 
        messages={messages} 
        streamingMessage={currentStreamingMessage}
      />
      <ChatInput 
        onSend={sendMessage} 
        disabled={isAgentRunning}
      />
    </div>
  )
}
```

### Derived State with Selectors

```typescript
// Create optimized selectors
export const useMessages = () => useChatStore(state => state.messages)
export const useAgentState = () => useChatStore(state => state.agentState)
export const useIsStreaming = () => useChatStore(state => 
  state.currentStreamingMessage.length > 0
)

// Component using selectors
function MessageList() {
  const messages = useMessages()
  const isStreaming = useIsStreaming()
  
  return (
    <div>
      {messages.map(msg => <Message key={msg.id} {...msg} />)}
      {isStreaming && <StreamingIndicator />}
    </div>
  )
}
```

### Persistence with Zustand Middleware

```typescript
import { persist } from 'zustand/middleware'

export const useChatStore = create(
  persist<ChatStore>(
    (set, get) => ({
      // ... store implementation
    }),
    {
      name: 'chat-storage',
      partialize: (state) => ({ 
        messages: state.messages,
        agentState: state.agentState
      })
    }
  )
)
```

### Tool Calls State Management

```typescript
interface ToolCall {
  id: string
  name: string
  args: string
  result?: any
  status: 'pending' | 'completed' | 'error'
}

interface ChatStoreWithTools extends ChatStore {
  toolCalls: Map<string, ToolCall>
  updateToolCall: (id: string, update: Partial<ToolCall>) => void
}

export const useChatStore = create<ChatStoreWithTools>((set) => ({
  // ... existing state
  toolCalls: new Map(),
  
  handleEvent: (event: BaseEvent) => {
    switch (event.type) {
      case 'TOOL_CALL_START':
        set(state => {
          const toolCalls = new Map(state.toolCalls)
          toolCalls.set(event.toolCallId, {
            id: event.toolCallId,
            name: event.toolCallName,
            args: '',
            status: 'pending'
          })
          return { toolCalls }
        })
        break
        
      case 'TOOL_CALL_ARGS':
        set(state => {
          const toolCalls = new Map(state.toolCalls)
          const current = toolCalls.get(event.toolCallId)
          if (current) {
            toolCalls.set(event.toolCallId, {
              ...current,
              args: current.args + event.delta
            })
          }
          return { toolCalls }
        })
        break
        
      case 'TOOL_CALL_RESULT':
        set(state => {
          const toolCalls = new Map(state.toolCalls)
          const current = toolCalls.get(event.toolCallId)
          if (current) {
            toolCalls.set(event.toolCallId, {
              ...current,
              result: event.content,
              status: 'completed'
            })
          }
          return { toolCalls }
        })
        break
    }
  },
  
  updateToolCall: (id, update) =>
    set(state => {
      const toolCalls = new Map(state.toolCalls)
      const current = toolCalls.get(id)
      if (current) {
        toolCalls.set(id, { ...current, ...update })
      }
      return { toolCalls }
    })
}))
```

---

## 8. How AG-UI Differs from Standard REST APIs

### Traditional REST API Pattern

```
Client → Request → Server → Response → Client
```

**Characteristics:**
- Synchronous request/response
- Single round-trip per interaction
- Stateless (or session-based)
- Predetermined response structure
- Pull-based (client requests data)

**Example:**
```typescript
// REST API
const response = await fetch('/api/chat', {
  method: 'POST',
  body: JSON.stringify({ message: 'Hello' })
})
const data = await response.json()
// Wait for complete response, then render
```

### AG-UI Event-Based Pattern

```
Client ←→ [Event Stream] ←→ Agent
       ↓
   Multiple Events (Streaming)
```

**Characteristics:**
- Asynchronous event streaming
- Multiple events per interaction
- Stateful conversations
- Dynamic response structure
- Push-based (server pushes updates)

**Example:**
```typescript
// AG-UI
agent.runAgent({ message: 'Hello' }).subscribe(event => {
  // Receive multiple events as they occur:
  // 1. RUN_STARTED
  // 2. TEXT_MESSAGE_START
  // 3. TEXT_MESSAGE_CONTENT (multiple times)
  // 4. TOOL_CALL_START
  // 5. TOOL_CALL_RESULT
  // 6. TEXT_MESSAGE_END
  // 7. RUN_FINISHED
})
```

### Key Differences

| Aspect | REST API | AG-UI Protocol |
|--------|----------|----------------|
| **Communication Model** | Request/Response | Event Streaming |
| **Directionality** | Unidirectional (per request) | Bidirectional |
| **State Management** | Stateless or session-based | Built-in state sync (snapshots + deltas) |
| **Response Timing** | Wait for completion | Real-time streaming |
| **Content Delivery** | Atomic (all at once) | Incremental (chunk by chunk) |
| **Tool Execution** | Hidden from client | Observable events |
| **Error Handling** | HTTP status codes | Structured error events |
| **Progress Tracking** | Not standardized | Built-in lifecycle events |
| **Intermediate Results** | Not available | Exposed via step events |
| **Type Safety** | API-specific schemas | Standardized event types |
| **Transport** | HTTP only | HTTP SSE, WebSocket, Binary |
| **Agent Actions** | Opaque server logic | Transparent tool calls |
| **UI Control** | Client-driven only | Bidirectional (agent can influence UI) |

### When to Use Each

**Use REST API when:**
- Simple CRUD operations
- Stateless data fetching
- Predetermined response structure
- No need for streaming
- Traditional web services

**Use AG-UI when:**
- Agent-based interactions
- Real-time streaming needed
- Complex multi-step workflows
- Tool execution visibility required
- State synchronization critical
- Progressive UI updates important
- Building conversational interfaces
- Need transparency in agent reasoning

### Migration Pattern: REST → AG-UI

```typescript
// Before (REST)
async function askAgent(question: string) {
  const response = await fetch('/api/chat', {
    method: 'POST',
    body: JSON.stringify({ question })
  })
  return response.json()
}

// After (AG-UI)
function askAgent(question: string) {
  const agent = new HttpAgent({ url: '/api/agent' })
  
  return agent.runAgent({
    messages: [{ role: 'user', content: question }]
  }).subscribe({
    next: (event) => {
      // Handle streaming events
      handleAgentEvent(event)
    },
    error: (err) => handleError(err),
    complete: () => handleComplete()
  })
}
```

---

## Implementation Recommendations for HR Chat Agent

### 1. **Architecture Setup**

```typescript
// Store structure
interface HRChatStore {
  // Conversation
  messages: Message[]
  currentStreamingMessage: string
  
  // Agent state
  agentState: {
    employeeContext?: Employee
    currentTask?: Task
    permissions?: string[]
  }
  
  // Tools & Activities
  toolCalls: Map<string, ToolCall>
  activities: Map<string, Activity>
  
  // UI state
  isAgentRunning: boolean
  error: string | null
  
  // Actions
  handleEvent: (event: BaseEvent) => void
  sendMessage: (content: string) => Promise<void>
  clearError: () => void
}
```

### 2. **Event Handler Pattern**

```typescript
const handleEvent = (event: BaseEvent) => {
  switch (event.type) {
    case 'RUN_STARTED':
      logAgentStart(event)
      break
    case 'TEXT_MESSAGE_CONTENT':
      appendStreamingContent(event.delta)
      break
    case 'TOOL_CALL_START':
      showToolExecution(event.toolCallName)
      break
    case 'STATE_SNAPSHOT':
      syncEmployeeContext(event.snapshot)
      break
    // ... handle all relevant event types
  }
}
```

### 3. **CopilotKit Integration (Recommended)**

For production-ready implementation, consider using **CopilotKit** (the primary AG-UI client):

```bash
npx copilotkit@latest init
```

```typescript
import { CopilotKit } from "@copilotkit/react-core"
import { CopilotChat } from "@copilotkit/react-ui"

function HRChatApp() {
  return (
    <CopilotKit 
      publicApiKey="your-api-key"
      // or connect to your own AG-UI agent
      agent={{ url: "/api/hr-agent" }}
    >
      <CopilotChat
        instructions="You are an HR assistant..."
        labels={{
          title: "HR Assistant",
          initial: "Hi! How can I help with HR matters?"
        }}
      />
    </CopilotKit>
  )
}
```

### 4. **Supported Agent Frameworks**

AG-UI integrates with:
- **LangGraph** (LangChain)
- **CrewAI**
- **Mastra** (TypeScript)
- **Pydantic AI** (Python)
- **Microsoft Agent Framework**
- **Google ADK**
- **AWS Strands**
- **LlamaIndex**
- Direct LLM (OpenAI, Anthropic, etc.)

### 5. **Protocol Benefits for HR Chat Agent**

- **Transparency**: See when agent searches policies, queries databases
- **Tool Visibility**: Display HR system queries as they happen
- **State Sync**: Keep employee context synchronized
- **Progress Tracking**: Show multi-step HR workflows
- **Error Handling**: Graceful handling of permission errors, data access issues
- **Streaming**: Display long HR policy answers progressively

---

## Additional Resources

- **AG-UI Specification**: https://docs.ag-ui.com
- **CopilotKit Docs**: https://docs.copilotkit.ai
- **AG-UI GitHub**: https://github.com/ag-ui-protocol/ag-ui
- **Discord Community**: https://discord.gg/Jd3FzfdJa8
- **Demo Applications**: https://dojo.ag-ui.com

---

## Conclusion

AG-UI provides a robust, standardized protocol for frontend-agent communication that addresses the unique challenges of building user-facing AI applications. Its event-based architecture, combined with efficient state synchronization and flexible transport options, makes it ideal for building modern conversational interfaces like the HR chat agent.

The protocol's integration with React/TypeScript and state management libraries like Zustand, along with production-ready clients like CopilotKit, provides a clear path from prototype to production deployment.
