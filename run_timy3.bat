@echo off
echo Building and running Timy3 application...

REM Try to find MSBuild in common locations
set MSBUILD_PATH=MSBuild.exe
if not exist "%MSBUILD_PATH%" (
    set MSBUILD_PATH="C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"
)
if not exist "%MSBUILD_PATH%" (
    set MSBUILD_PATH="C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe"
)
if not exist "%MSBUILD_PATH%" (
    set MSBUILD_PATH="C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin\MSBuild.exe"
)

REM Navigate to project directory
cd AlgeTimyUsb.SampleApplicationCSharp

REM Try to build the project
if exist %MSBUILD_PATH% (
    echo Building project with %MSBUILD_PATH%...
    %MSBUILD_PATH% /p:Configuration=Debug
    if %ERRORLEVEL% NEQ 0 (
        echo Build failed! Error code: %ERRORLEVEL%
        pause
        exit /b %ERRORLEVEL%
    )
) else (
    echo MSBuild not found. Skipping build step.
)

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