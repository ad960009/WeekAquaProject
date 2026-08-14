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
if exist "%OUTPUT_DIR%\SelfContained\*.pdb" del /f /q "%OUTPUT_DIR%\SelfContained\*.pdb"

echo.
echo [2/2] Building Framework-Dependent Single File (Lightweight)...
dotnet publish "%PROJECT_DIR%WeekAquaWPF.csproj" -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=false -p:IncludeNativeLibrariesForSelfExtract=true -o "%OUTPUT_DIR%\FrameworkDependent"
if errorlevel 1 (
    echo.
    echo [ERROR] Failed to build Framework-Dependent executable.
    pause
    exit /b %errorlevel%
)
if exist "%OUTPUT_DIR%\FrameworkDependent\*.pdb" del /f /q "%OUTPUT_DIR%\FrameworkDependent\*.pdb"

echo.
echo Packaging Release ZIPs using tar (PDBs Excluded)...
tar -a -c -f "%OUTPUT_DIR%\WeekAquaWPF-win-x64-SelfContained.zip" -C "%OUTPUT_DIR%\SelfContained" WeekAquaWPF.exe
tar -a -c -f "%OUTPUT_DIR%\WeekAquaWPF-win-x64-FrameworkDependent.zip" -C "%OUTPUT_DIR%\FrameworkDependent" WeekAquaWPF.exe

echo.
echo =================================================
echo  Build and Package Complete! (PDBs Excluded)
echo  - Self-Contained Exe:       %OUTPUT_DIR%\SelfContained\WeekAquaWPF.exe
echo  - Framework-Dependent Exe:  %OUTPUT_DIR%\FrameworkDependent\WeekAquaWPF.exe
echo  - Self-Contained ZIP:       %OUTPUT_DIR%\WeekAquaWPF-win-x64-SelfContained.zip
echo  - Framework-Dependent ZIP:  %OUTPUT_DIR%\WeekAquaWPF-win-x64-FrameworkDependent.zip
echo =================================================
echo.
pause
