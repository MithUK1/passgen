@echo off
rem Builds Passgen.exe (needs the .NET SDK) and the extension for each browser (needs Node.js).
setlocal
cd /d "%~dp0"
dotnet build app\Passgen.csproj -c Release --nologo
if errorlevel 1 exit /b 1
copy /y app\bin\Release\net48\Passgen.exe Passgen.exe >nul
echo Built Passgen.exe
where node >nul 2>nul
if errorlevel 1 (
  echo Node.js is not installed, so the extension was not built.
  exit /b 0
)
node tools\build.mjs
