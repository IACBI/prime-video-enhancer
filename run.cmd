@echo off
rem publish.cmd writes to publish\Light and publish\Standalone, never to
rem publish\ directly, so the old single path here could never match and this
rem always fell through to a source build.
set "LIGHT_EXE=%~dp0publish\Light\PrimeVideoSpeedApp.exe"
set "STANDALONE_EXE=%~dp0publish\Standalone\PrimeVideoSpeedApp.exe"

if exist "%LIGHT_EXE%" (
  "%LIGHT_EXE%"
) else if exist "%STANDALONE_EXE%" (
  "%STANDALONE_EXE%"
) else (
  dotnet run --project "%~dp0PrimeVideoSpeedApp.csproj"
)
