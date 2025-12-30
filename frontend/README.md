# HR Chat Agent - Frontend

Modern React frontend for the HR Chat Agent timesheet management system.

## Overview

This frontend provides a conversational interface for employees to manage their timesheets through natural language interactions. Built with React 18, TypeScript, Vite, and shadcn/ui components.

## Features

- **Conversational UI**: Chat-based interface for timesheet management
- **Real-time Updates**: Server-Sent Events (SSE) for live agent responses
- **Modern Stack**: React 18, TypeScript, Vite, TailwindCSS
- **Component Library**: shadcn/ui for consistent, accessible UI components
- **State Management**: Zustand for lightweight, efficient state management
- **Timezone Detection**: Automatic browser timezone detection for accurate timestamps

## Tech Stack

- **React 18** - UI framework
- **TypeScript** - Type safety
- **Vite** - Fast build tool and dev server
- **TailwindCSS** - Utility-first CSS
- **shadcn/ui** - High-quality React components
- **Zustand** - State management
- **ESLint + Prettier** - Code quality

## Development

\`\`\`bash
# Install dependencies
npm install

# Start dev server (http://localhost:5173)
npm run dev

# Build for production
npm run build

# Preview production build
npm run preview

# Lint code
npm run lint
\`\`\`

## Project Structure

\`\`\`
frontend/
├── src/
│   ├── components/       # React components
│   │   ├── chat/         # Chat interface components
│   │   ├── profile/      # User profile components
│   │   └── ui/           # shadcn/ui components
│   ├── store/            # Zustand stores
│   ├── services/         # API clients
│   ├── utils/            # Utility functions
│   └── App.tsx           # Main application
├── public/               # Static assets
└── package.json
\`\`\`

## Environment Variables

Create a \`.env\` file:

\`\`\`env
VITE_API_URL=http://localhost:5000
\`\`\`

## Integration with Backend

The frontend communicates with the HRAgent.Api backend via:
- REST API for queries
- Server-Sent Events (SSE) for streaming conversation responses
- AG-UI protocol for structured agent communication

See [../specs/001-hr-chat-agent/contracts/ag-ui-protocol.md](../specs/001-hr-chat-agent/contracts/ag-ui-protocol.md) for protocol details.

## Learn More

- [Vite Documentation](https://vitejs.dev/)
- [React Documentation](https://react.dev/)
- [shadcn/ui](https://ui.shadcn.com/)
- [TailwindCSS](https://tailwindcss.com/)
