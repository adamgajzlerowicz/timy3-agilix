# Timy3 Connector Application

This application connects to an Alge Timy3 device via USB and retrieves readings from it.

## Prerequisites

1. Install the .NET SDK 6.0 or later
   - Download from: https://dotnet.microsoft.com/download

2. Microsoft Visual C++ 2022 Redistributable
   - Required for the Alge Timy3 driver
   - Download from: https://aka.ms/vs/17/release/vc_redist.x64.exe (64-bit)
   - Or: https://aka.ms/vs/17/release/vc_redist.x86.exe (32-bit)

## Setup Instructions

1. Clone or download this repository

2. Copy the required DLL files to the output directory:
   ```
   mkdir -p TimyConnector/bin/Debug/net6.0/
   cp driver/Dependencies/AlgeTimyUsb.x86.dll TimyConnector/bin/Debug/net6.0/
   cp driver/Dependencies/AlgeTimyUsb.x64.dll TimyConnector/bin/Debug/net6.0/
   cp driver/Dependencies/AlgeTimyUsb.Dummy.dll TimyConnector/bin/Debug/net6.0/
   ```

3. Build and run the application:
   ```
   cd TimyConnector
   dotnet build
   dotnet run
   ```

## Usage

1. Connect your Alge Timy3 device to your computer via USB
2. Start the application
3. The application will automatically detect and connect to the device
4. All messages from the device will be displayed in the console

## Notes

- This is a basic implementation that only connects to the device and displays messages
- Future versions will include a WebSocket server to forward signals to other applications 