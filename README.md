# Alge Timy3 USB Connector Project

This project provides tools to connect to an Alge Timy3 timing device via USB.

## Project Structure

- **driver/** - Contains the original Alge Timy3 USB driver files and sample applications
- **TimyConnector/** - A new .NET application to connect to the Timy3 device

## Getting Started

To use the TimyConnector application:

1. Install Prerequisites:
   - .NET SDK 6.0 or later (https://dotnet.microsoft.com/download)
   - Microsoft Visual C++ 2022 Redistributable

2. Navigate to the TimyConnector directory and run the setup script:
   - Windows: `setup.bat`
   - Linux/macOS: `./setup.sh` (make executable first with `chmod +x setup.sh`)

3. Build and run the application:
   ```
   cd TimyConnector
   dotnet build
   dotnet run
   ```

4. Connect your Alge Timy3 device via USB

## Future Development

See the [ROADMAP.md](TimyConnector/ROADMAP.md) file in the TimyConnector directory for future development plans, including:

- WebSocket server for forwarding signals
- Advanced data processing
- Multiple device support
- Distribution packages 