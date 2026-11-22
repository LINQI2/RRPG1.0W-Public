@echo off
rem ============================================================================
rem  RRPG Static Configuration - Producer entry 2/3 : Check configuration
rem  Read-only: compiler validate (proves Authoring == Canonical Mirror).
rem  No Unity needed. Exit code 0 = PASS, non-zero = FAIL (details printed).
rem  ASCII-only on purpose: Chinese guidance is printed by the compiler itself.
rem ============================================================================
chcp 65001 >nul
setlocal
set "REPO=%~dp0"
set "REPO1=%REPO:~0,-1%"
set "CFG=%REPO%Tools\RRPG.ConfigCompiler"
set "EXE=%CFG%\src\RRPG.ConfigCompiler\bin\Release\net10.0\rrpg-config.exe"

echo.
echo === RRPG Static Configuration : check (validate, read-only) ===
echo.

where dotnet >nul 2>&1
if errorlevel 1 (
  echo [BLOCKED] .NET SDK ^(dotnet^) not found on PATH. Install it, then run this again.
  goto :end
)

if not exist "%EXE%" (
  echo [1/2] Building the config compiler ^(first run may restore NuGet packages^) ...
  dotnet build "%CFG%\src\RRPG.ConfigCompiler\RRPG.ConfigCompiler.csproj" -c Release -v q
  if errorlevel 1 (
    echo [FAILED] compiler build failed. See the output above.
    goto :end
  )
)

echo [2/2] Validating configuration ...
"%EXE%" validate --root "%REPO1%"
set "CODE=%ERRORLEVEL%"

echo.
if "%CODE%"=="0" (
  echo RESULT: PASS ^(no file was written; mirror matches authoring^).
) else (
  echo RESULT: FAILED ^(exit=%CODE%^). Nothing was written.
  echo   Fix the reported items, then run this entry again.
  echo   Full report: Tools\RRPG.ConfigCompiler\.build-state\  ^(after a build^)
)
echo.

:end
pause
exit /b 0
