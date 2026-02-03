@echo off
title University Auto Setup v3.0
color 1F

echo.
echo  ===============================================================
echo                   University Auto Setup v3.0
echo               Appalachian State University
echo  ===============================================================
echo.

:: Check for admin rights and elevate if needed
net session >nul 2>&1
if %errorLevel% == 0 (
    echo  [OK] Administrative privileges confirmed
    echo.
    echo  Starting application...
    echo.
    start "" "%~dp0AutoSetup-GUI.exe"
    exit /b 0
) else (
    echo  [!] Administrative privileges required
    echo.
    echo  Requesting elevation...
    powershell.exe -Command "Start-Process '%~f0' -Verb RunAs"
    exit /b 0
)
