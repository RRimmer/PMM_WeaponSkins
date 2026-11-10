@echo off
rem Builds PMM_WeaponSkins in Release and writes the log next to this file.
cd /d "%~dp0"
dotnet build PMM_WeaponSkins.csproj -c Release -nologo > build.log 2>&1
echo exit %ERRORLEVEL% >> build.log
