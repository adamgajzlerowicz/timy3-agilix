@echo off
echo Setting up Timy3 Connector Application
echo --------------------------------------

echo Creating output directories...
mkdir bin\Debug\net6.0

echo Copying driver files...
copy ..\driver\Dependencies\AlgeTimyUsb.x86.dll bin\Debug\net6.0\
copy ..\driver\Dependencies\AlgeTimyUsb.x64.dll bin\Debug\net6.0\
copy ..\driver\Dependencies\AlgeTimyUsb.Dummy.dll bin\Debug\net6.0\

echo Setup completed!
echo.
echo To build and run the application, make sure you have .NET SDK installed, then run:
echo   dotnet build
echo   dotnet run
echo.
echo Press any key to exit...
pause > nul 