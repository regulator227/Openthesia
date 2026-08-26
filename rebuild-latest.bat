@echo off
setlocal EnableExtensions

set "REPO=%~dp0"
if "%REPO:~-1%"=="\" set "REPO=%REPO:~0,-1%"
set "PROJECT=%REPO%\Openthesia\Openthesia.csproj"
set "EXE=%REPO%\Openthesia\bin\x64\Release\net6.0\Openthesia.exe"

title Rebuild latest Openthesia

where git >nul 2>&1
if errorlevel 1 (
    echo ERROR: Git was not found.
    goto :failed
)

where dotnet >nul 2>&1
if errorlevel 1 (
    echo ERROR: .NET SDK was not found.
    goto :failed
)

for /f "delims=" %%B in ('git -C "%REPO%" branch --show-current') do set "BRANCH=%%B"
if not defined BRANCH (
    echo ERROR: This checkout is not on a named branch.
    goto :failed
)

git -C "%REPO%" diff --quiet
if errorlevel 1 (
    echo ERROR: There are uncommitted tracked-file changes.
    echo Commit or stash them before rebuilding.
    goto :failed
)

git -C "%REPO%" diff --cached --quiet
if errorlevel 1 (
    echo ERROR: There are staged changes.
    echo Commit or unstage them before rebuilding.
    goto :failed
)

echo Fetching the latest fork master...
git -C "%REPO%" fetch origin master
if errorlevel 1 goto :failed

if /I "%BRANCH%"=="master" (
    git -C "%REPO%" merge --ff-only origin/master
    if errorlevel 1 (
        echo ERROR: Local master could not be safely fast-forwarded.
        goto :failed
    )
) else (
    git -C "%REPO%" merge-base --is-ancestor origin/master HEAD
    if errorlevel 1 (
        echo ERROR: Branch %BRANCH% does not contain the latest origin/master.
        echo Update the branch from master before rebuilding.
        goto :failed
    )
)

for /f "delims=" %%H in ('git -C "%REPO%" rev-parse HEAD') do set "LOCAL_COMMIT=%%H"
for /f "delims=" %%H in ('git -C "%REPO%" rev-parse origin/master') do set "REMOTE_COMMIT=%%H"

if /I "%BRANCH%"=="master" (
    if /I not "%LOCAL_COMMIT%"=="%REMOTE_COMMIT%" (
        echo ERROR: Local master does not exactly match origin/master.
        goto :failed
    )
)

echo.
echo Building Release x64...
dotnet build "%PROJECT%" -c Release -p:Platform=x64
if errorlevel 1 goto :failed

if not exist "%EXE%" (
    echo ERROR: Build succeeded, but the executable was not found.
    goto :failed
)

echo.
echo ============================================================
echo BUILD SUCCEEDED
echo Branch:    %BRANCH%
echo Commit:    %LOCAL_COMMIT%
echo Master:    %REMOTE_COMMIT%
for %%F in ("%EXE%") do echo Built:     %%~tF
echo Executable:
echo %EXE%
echo ============================================================

if /I "%~1"=="run" (
    echo.
    echo Launching Openthesia...
    start "" "%EXE%"
)

echo.
pause
exit /b 0

:failed
echo.
echo BUILD FAILED. No executable was launched.
echo.
pause
exit /b 1
