@echo off
setlocal EnableExtensions EnableDelayedExpansion

rem ============================================================================
rem  Pluck — Build Portable + Setup (may chay tren may sach)
rem  Output: %~dp0Pluck-1.0.1-Portable\  va  %~dp0Pluck-1.0.1-Setup.exe
rem ============================================================================

chcp 65001 >nul
title Pluck Build

set "PRODUCT_DIR=%~dp0"
for %%I in ("%PRODUCT_DIR%..") do set "ROOT_DIR=%%~fI"

set "VERSION=1.0.1"
set "PORTABLE_DIR=%PRODUCT_DIR%Pluck-%VERSION%-Portable"
set "SETUP_EXE=%PRODUCT_DIR%Pluck-%VERSION%-Setup.exe"
set "INNO_DIR=%ROOT_DIR%\tools\InnoSetup6"
set "ISCC=%INNO_DIR%\ISCC.exe"
set "INNO_INSTALLER=%TEMP%\pluck-innosetup-installer.exe"
set "PROJECT=%ROOT_DIR%\Pluck.UI\Pluck.UI.csproj"
set "ISS_FILE=%ROOT_DIR%\installer\Pluck.iss"

echo.
echo ========================================
echo   Pluck Build  v%VERSION%
echo ========================================
echo   Root:    %ROOT_DIR%
echo   Product: %PRODUCT_DIR%
echo.

rem --- .NET SDK ---
where dotnet >nul 2>&1
if errorlevel 1 (
    echo [INFO] Chua co dotnet CLI. Dang cai .NET SDK 10 qua winget...
    where winget >nul 2>&1
    if errorlevel 1 (
        echo [LOI] Khong tim thay winget. Hay cai .NET 10 SDK:
        echo       https://dotnet.microsoft.com/download/dotnet/10.0
        goto :fail
    )
    winget install --id Microsoft.DotNet.SDK.10 -e --accept-package-agreements --accept-source-agreements
    if errorlevel 1 (
        echo [LOI] Cai .NET SDK that bai.
        goto :fail
    )
    rem Refresh PATH for current session
    set "PATH=%PATH%;%ProgramFiles%\dotnet;%ProgramFiles(x86)%\dotnet"
    where dotnet >nul 2>&1
    if errorlevel 1 (
        echo [LOI] Da cai SDK nhung chua thay dotnet trong PATH.
        echo       Dong cua so CMD moi va chay lai build.bat
        goto :fail
    )
)

for /f "delims=" %%V in ('dotnet --version 2^>nul') do set "DOTNET_VER=%%V"
echo [OK] dotnet %DOTNET_VER%

rem --- Inno Setup (ISCC) ---
if not exist "%ISCC%" (
    echo [INFO] Chua co Inno Setup. Dang tai va cai vao tools\InnoSetup6 ...
    if not exist "%ROOT_DIR%\tools" mkdir "%ROOT_DIR%\tools"

    powershell -NoProfile -ExecutionPolicy Bypass -Command ^
        "$ProgressPreference='SilentlyContinue';" ^
        "Invoke-WebRequest -Uri 'https://jrsoftware.org/download.php/is.exe' -OutFile '%INNO_INSTALLER%' -UseBasicParsing"
    if errorlevel 1 (
        echo [LOI] Tai Inno Setup that bai. Kiem tra mang.
        goto :fail
    )

    "%INNO_INSTALLER%" /VERYSILENT /SUPPRESSMSGBOXES /DIR="%INNO_DIR%"
    if errorlevel 1 (
        echo [LOI] Cai Inno Setup that bai.
        goto :fail
    )
    if not exist "%ISCC%" (
        echo [LOI] Khong tim thay ISCC sau khi cai Inno Setup.
        goto :fail
    )
)
echo [OK] Inno Setup: %ISCC%

rem --- Dung Pluck neu dang chay (tranh khoa file) ---
tasklist /FI "IMAGENAME eq Pluck.exe" 2>nul | find /I "Pluck.exe" >nul
if not errorlevel 1 (
    echo [INFO] Dang dong Pluck.exe...
    taskkill /IM Pluck.exe /F >nul 2>&1
    timeout /t 2 /nobreak >nul
)

rem --- Publish Portable ---
echo.
echo [1/3] Publish portable (win-x64, single-file, compressed)...
if exist "%PORTABLE_DIR%" rmdir /s /q "%PORTABLE_DIR%"

dotnet publish "%PROJECT%" ^
    -c Release ^
    -p:PublishProfile=Portable ^
    -o "%PORTABLE_DIR%"

if errorlevel 1 (
    echo [LOI] dotnet publish that bai.
    goto :fail
)

if not exist "%PORTABLE_DIR%\Pluck.exe" (
    echo [LOI] Khong tim thay Pluck.exe sau publish.
    goto :fail
)
echo [OK] Portable: %PORTABLE_DIR%

rem --- README ---
echo [2/3] Tao README.txt...
(
echo Pluck %VERSION% — Portable
echo ======================
echo.
echo Chay truc tiep, khong can cai dat.
echo.
echo 1. Mo thu muc nay
echo 2. Double-click Pluck.exe
echo 3. Ung dung chay o system tray
echo.
echo Du lieu luu tai: %%LocalAppData%%\Pluck\
echo.
echo Yeu cau: Windows 10/11 ^(64-bit^)
) > "%PORTABLE_DIR%\README.txt"

rem --- Build Setup.exe ---
echo [3/3] Build installer...
if exist "%SETUP_EXE%" del /f /q "%SETUP_EXE%"

"%ISCC%" "%ISS_FILE%"
if errorlevel 1 (
    echo [LOI] Inno Setup compile that bai.
    goto :fail
)

if not exist "%SETUP_EXE%" (
    echo [LOI] Khong tim thay file Setup sau khi build.
    goto :fail
)

echo.
echo ========================================
echo   BUILD THANH CONG
echo ========================================
echo   Portable: %PORTABLE_DIR%
echo   Setup:    %SETUP_EXE%
echo ========================================
echo.
goto :end

:fail
echo.
echo ========================================
echo   BUILD THAT BAI
echo ========================================
echo.
exit /b 1

:end
endlocal
exit /b 0
