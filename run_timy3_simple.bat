@echo off
echo Running Timy3 application...

REM Navigate to project directory
cd AlgeTimyUsb.SampleApplicationCSharp

REM Check if executable exists
if not exist "bin\Debug\AlgeTimyUsb.SampleApplication.exe" (
    echo Executable not found! Make sure the project is built correctly.
    pause
    exit /b 1
)

REM Run the application
echo Starting Timy3 application...
start "" "bin\Debug\AlgeTimyUsb.SampleApplication.exe"

exit /b 0 