# Development Roadmap for Timy3 Connector

## Phase 1: Basic Connection (Current)
- Establish USB connection to Alge Timy3 device
- Receive and display data from the device
- Basic error handling

## Phase 2: WebSocket Server
- Implement WebSocket server to forward device signals
- Create a simple protocol for clients to connect and receive data
- Add configuration options for WebSocket server (port, etc.)

## Phase 3: Data Processing
- Parse and interpret different message types from the device
- Store readings in a structured format
- Add filtering options for different types of readings

## Phase 4: Advanced Features
- Multiple device support
- Data logging to file
- Replay capabilities
- User interface (optional)

## Phase 5: Distribution
- Package the application for easy distribution
- Create installer for Windows
- Create Docker container for server deployment 