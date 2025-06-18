@echo off
echo Building and running Timy3 application...

REM Navigate to project directory first
cd AlgeTimyUsb.SampleApplicationCSharp

REM Clean the build output first to ensure fresh build
echo Cleaning previous build...
if exist "bin\Debug" (
    rmdir /s /q "bin\Debug"
    echo Previous build cleaned.
)
if exist "obj\Debug" (
    rmdir /s /q "obj\Debug"
    echo Previous obj files cleaned.
)

REM Try to find MSBuild in common locations (with proper quoting)
set "MSBUILD_PATH=MSBuild.exe"
if not exist "%MSBUILD_PATH%" (
    set "MSBUILD_PATH=C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"
)
if not exist "%MSBUILD_PATH%" (
    set "MSBUILD_PATH=C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe"
)
if not exist "%MSBUILD_PATH%" (
    set "MSBUILD_PATH=C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin\MSBuild.exe"
)
if not exist "%MSBUILD_PATH%" (
    set "MSBUILD_PATH=C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe"
)
if not exist "%MSBUILD_PATH%" (
    set "MSBUILD_PATH=C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe"
)

REM Try to build the project
if exist "%MSBUILD_PATH%" (
    echo Building project with "%MSBUILD_PATH%"...
    echo Build command: "%MSBUILD_PATH%" /p:Configuration=Debug /p:Platform=x86 /t:Rebuild
    "%MSBUILD_PATH%" /p:Configuration=Debug /p:Platform=x86 /t:Rebuild "AlgeTimyUsb.SampleApplicationCSharp.csproj"
    if %ERRORLEVEL% NEQ 0 (
        echo Build failed! Error code: %ERRORLEVEL%
        pause
        exit /b %ERRORLEVEL%
    )
    echo Build completed successfully!
) else (
    echo MSBuild not found. The project has dependency issues that prevent dotnet build from working.
    echo Please open the project in Visual Studio and build it manually.
    echo The issue is with the pre-build event copying DLL files.
    pause
    exit /b 1
)

REM Check if executable exists
if not exist "bin\Debug\AlgeTimyUsb.SampleApplication.exe" (
    echo Executable not found! Build may have failed.
    echo Looking for files in bin\Debug:
    if exist "bin\Debug" (
        dir "bin\Debug"
    ) else (
        echo bin\Debug directory does not exist!
    )
    pause
    exit /b 1
)

REM Show file timestamp to confirm it's fresh
echo Executable found with timestamp:
dir "bin\Debug\AlgeTimyUsb.SampleApplication.exe"

REM Run the application in foreground so we can see any errors
echo Starting Timy3 application...
echo If the application doesn't appear, check for error dialogs.
echo Running: "bin\Debug\AlgeTimyUsb.SampleApplication.exe"
"bin\Debug\AlgeTimyUsb.SampleApplication.exe"

REM If we get here, the application has closed
echo Application has terminated.
pause

exit /b 0 