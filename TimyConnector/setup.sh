#!/bin/bash

echo "Setting up Timy3 Connector Application"
echo "--------------------------------------"

echo "Creating output directories..."
mkdir -p bin/Debug/net6.0

echo "Copying driver files..."
cp ../driver/Dependencies/AlgeTimyUsb.x86.dll bin/Debug/net6.0/
cp ../driver/Dependencies/AlgeTimyUsb.x64.dll bin/Debug/net6.0/
cp ../driver/Dependencies/AlgeTimyUsb.Dummy.dll bin/Debug/net6.0/

echo "Setup completed!"
echo
echo "To build and run the application, make sure you have .NET SDK installed, then run:"
echo "  dotnet build"
echo "  dotnet run" 