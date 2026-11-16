@echo off
setlocal
cd /d "%~dp0"
dotnet publish Rentt.csproj -c Release -r win-x64 --self-contained true -o publish -p:PublishTrimmed=false -p:DebugType=None -p:DebugSymbols=false
if errorlevel 1 exit /b 1
set "INNO_COMPILER=%ProgramFiles%\Inno Setup 7\ISCC.exe"
if not exist "%INNO_COMPILER%" set "INNO_COMPILER=%ProgramFiles(x86)%\Inno Setup 7\ISCC.exe"
if not exist "%INNO_COMPILER%" (
  echo Inno Setup 7 not found. Install it or compile installer\RentSport.iss manually.
  exit /b 1
)
"%INNO_COMPILER%" installer\RentSport.iss
exit /b %errorlevel%
