@echo off
setlocal enabledelayedexpansion

REM ------------------------------------------------------------
REM Build and run Unity project only when build succeeds.
REM ------------------------------------------------------------

set "ROOT=%~dp0"
set "PROJECT_PATH=%ROOT%"
if "%PROJECT_PATH:~-1%"=="\" set "PROJECT_PATH=%PROJECT_PATH:~0,-1%"
set "BUILD_DIR=%ROOT%Build"
set "BUILD_EXE=%BUILD_DIR%\Metroidvania.exe"
set "BUILD_LOG=%ROOT%unity_build_auto.log"

REM Allow overriding Unity path from environment variable UNITY_EXE.
if not defined UNITY_EXE set "UNITY_EXE=D:\Program Files\Unity 2022.3.62f3c1\Editor\Unity.exe"

if not exist "%UNITY_EXE%" (
    echo [ERROR] Unity executable not found: "%UNITY_EXE%"
    echo [HINT] Set UNITY_EXE before running this script.
    echo Example:
    echo   set UNITY_EXE=D:\Path\To\Unity.exe
    exit /b 2
)

echo [INFO] Unity executable: "%UNITY_EXE%"
echo [INFO] Project path: "%PROJECT_PATH%"
echo [INFO] Build log: "%BUILD_LOG%"
echo [INFO] Building project...

"%UNITY_EXE%" -batchmode -nographics -quit -projectPath "%PROJECT_PATH%" -executeMethod CIBuilder.BuildWindows -logFile "%BUILD_LOG%"
set "BUILD_EXIT=%ERRORLEVEL%"

if not "%BUILD_EXIT%"=="0" (
    echo [ERROR] Build failed with exit code %BUILD_EXIT%.
    echo [INFO] Game will NOT be launched.
    echo [INFO] Check log: "%BUILD_LOG%"
    exit /b %BUILD_EXIT%
)

if not exist "%BUILD_EXE%" (
    echo [ERROR] Build reported success but executable was not found:
    echo         "%BUILD_EXE%"
    echo [INFO] Game will NOT be launched.
    echo [INFO] Check log: "%BUILD_LOG%"
    exit /b 3
)

echo [INFO] Build succeeded.
echo [INFO] Launching game: "%BUILD_EXE%"
start "Metroidvania" "%BUILD_EXE%"

exit /b 0
