@echo off
rem Builds Passgen.exe from src. Needs the .NET SDK.
setlocal
cd /d "%~dp0"
dotnet build src\Passgen.csproj -c Release --nologo
if errorlevel 1 exit /b 1
copy /y src\bin\Release\net48\Passgen.exe Passgen.exe >nul
echo Built Passgen.exe
