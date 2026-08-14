@echo off
setlocal
echo =================================================
echo  Building WeekAqua WPF Single-File Executables
echo =================================================

set "PROJECT_DIR=%~dp0"
set "OUTPUT_DIR=%PROJECT_DIR%publish"

if exist "%OUTPUT_DIR%" (
    echo Cleaning previous publish directory...
    rmdir /s /q "%OUTPUT_DIR%"
)

echo.
echo [1/2] Building Self-Contained Single File (Runtime Included)...
dotnet publish "%PROJECT_DIR%WeekAquaWPF.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "%OUTPUT_DIR%\SelfContained"
if errorlevel 1 (
    echo.
    echo [ERROR] Failed to build Self-Contained executable.
    pause
    exit /b %errorlevel%
)

echo.
echo [2/2] Building Framework-Dependent Single File (Lightweight)...
dotnet publish "%PROJECT_DIR%WeekAquaWPF.csproj" -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=false -p:IncludeNativeLibrariesForSelfExtract=true -o "%OUTPUT_DIR%\FrameworkDependent"
if errorlevel 1 (
    echo.
    echo [ERROR] Failed to build Framework-Dependent executable.
    pause
    exit /b %errorlevel%
)

echo.
echo =================================================
echo  Build Complete! Single-file binaries created:
echo  - Self-Contained:       %OUTPUT_DIR%\SelfContained\WeekAquaWPF.exe
echo  - Framework-Dependent:  %OUTPUT_DIR%\FrameworkDependent\WeekAquaWPF.exe
echo =================================================
echo.
pause
