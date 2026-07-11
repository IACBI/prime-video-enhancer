@echo off
echo ===============================================================================
echo   Prime Video Enhancer - Single-File Publish Builder for GitHub Releases
echo ===============================================================================
echo.

echo [1/2] Building Light Single-File EXE (Framework-Dependent, ~213 KB)...
dotnet publish PrimeVideoSpeedApp.csproj -c Release --self-contained false -p:PublishSingleFile=true -p:SelfContained=false -o publish\Light
if %errorlevel% neq 0 (
    echo [ERROR] Light build failed.
    pause
    exit /b %errorlevel%
)
echo --^> Light build completed successfully: publish\Light\PrimeVideoSpeedApp.exe (~213 KB)
echo.

echo [2/2] Building Standalone Single-File EXE (Self-Contained, ~64 MB)...
dotnet publish PrimeVideoSpeedApp.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish\Standalone
if %errorlevel% neq 0 (
    echo [ERROR] Standalone build failed.
    pause
    exit /b %errorlevel%
)
echo --^> Standalone build completed successfully: publish\Standalone\PrimeVideoSpeedApp.exe (~64 MB)
echo.

echo ===============================================================================
echo   All GitHub Release builds completed successfully!
echo   Output Directory: publish\
echo ===============================================================================
echo.
pause
