@echo off
setlocal
cd /d "%~dp0\.."

echo Publishing BlackBoxIdentification for Windows x64...
dotnet publish "src\BlackBoxIdentification\BlackBoxIdentification.csproj" ^
  -c Release ^
  -r win-x64 ^
  --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:PublishTrimmed=false ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:DebugType=None ^
  -o "publish\win-x64"

if errorlevel 1 (
  echo.
  echo Publishing failed.
  pause
  exit /b 1
)

echo.
echo Done. Published files are in:
echo %CD%\publish\win-x64
pause
