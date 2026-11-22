@echo off
rem ============================================================================
rem  RRPG Static Configuration - M0 Toolchain verification (repeatable)
rem  Steps: compiler build -> compiler tests -> validate -> git rules -> Unity
rem  Unity steps run only when an editor executable is resolvable, otherwise they
rem  are reported as SKIPPED (explicitly, never as PASS).
rem  ASCII-only on purpose (PS 5.1 / cmd encoding safety).
rem ============================================================================
chcp 65001 >nul
setlocal enabledelayedexpansion
set "REPO=%~dp0..\.."
pushd "%REPO%"
set "REPO=%CD%"
set "CFG=%REPO%\Tools\RRPG.ConfigCompiler"
set "PROJ=%REPO%\UnityProject\RRPG1.0game"
set "STATE=%CFG%\.build-state"
set "FAILED=0"

echo.
echo === RRPG Static Configuration - M0 toolchain verification ===
echo repo: %REPO%
echo.

if not exist "%STATE%" mkdir "%STATE%"

echo [1/7] dotnet SDK ...
where dotnet >nul 2>&1
if errorlevel 1 (echo   FAILED: dotnet not found & set "FAILED=1" & goto :unity)

echo [2/7] compiler build (Release) ...
dotnet build "%CFG%\src\RRPG.ConfigCompiler\RRPG.ConfigCompiler.csproj" -c Release -v q
if errorlevel 1 (echo   FAILED: compiler build & set "FAILED=1" & goto :unity)
echo   PASS

echo [3/7] compiler tests (xUnit) ...
dotnet test "%CFG%\tests\RRPG.ConfigCompiler.Tests\RRPG.ConfigCompiler.Tests.csproj" -c Release -v q
if errorlevel 1 (echo   FAILED: compiler tests & set "FAILED=1" & goto :unity)
echo   PASS

echo [4/7] rrpg-config validate (read-only) ...
"%CFG%\src\RRPG.ConfigCompiler\bin\Release\net10.0\rrpg-config.exe" validate --root "%REPO%"
if errorlevel 1 (echo   FAILED: validate & set "FAILED=1" & goto :unity)
echo   PASS

:unity
echo [5/7] git rules - generated root must be ignored ...
git -C "%REPO%" check-ignore -q "UnityProject/RRPG1.0game/Assets/Generated/StaticConfig/Resources/RRPGStaticConfigRoot.asset"
if errorlevel 1 (echo   FAILED: Assets/Generated/StaticConfig/** is NOT ignored & set "FAILED=1") else (echo   PASS)

echo [6/7] git rules - canonical mirror must be trackable ...
git -C "%REPO%" check-ignore -q "Development/Generated/StaticConfig/Monster.json"
if errorlevel 1 (echo   PASS) else (echo   FAILED: Development/Generated/StaticConfig/** is ignored & set "FAILED=1")
git -C "%REPO%" check-attr text eol -- "Development/Generated/StaticConfig/Monster.json"

echo [7/7] Unity batchmode (EditMode) ...
set "EDITOR="
if defined RRPG_TUANJIE_EXE set "EDITOR=%RRPG_TUANJIE_EXE%"
if not defined EDITOR if exist "%CFG%\Producer\editor-path.local.txt" for /f "usebackq delims=" %%E in ("%CFG%\Producer\editor-path.local.txt") do set "EDITOR=%%E"
if not defined EDITOR if exist "D:\BBB\Tuanjie\Hub\Editor\2022.3.62t9\Editor\Tuanjie.exe" set "EDITOR=D:\BBB\Tuanjie\Hub\Editor\2022.3.62t9\Editor\Tuanjie.exe"

if not defined EDITOR (
  echo   SKIPPED: no Unity editor resolvable (set RRPG_TUANJIE_EXE). Unity-side gates are NOT verified.
) else (
  if exist "%PROJ%\Temp\UnityLockfile" (
    echo   SKIPPED: Unity editor is open for this project.
  ) else (
    "%EDITOR%" -batchmode -quit -projectPath "%PROJ%" -executeMethod RRPG.StaticConfig.EditorTools.StaticConfigBuilder.ExecuteBuild -logFile "%STATE%\unity-builder.log"
    if exist "%STATE%\builder.stage.txt" (type "%STATE%\builder.stage.txt") else (echo   FAILED: builder.stage.txt missing & set "FAILED=1")
    if exist "%PROJ%\Temp\UnityLockfile" (
      echo   NOTE: editor may still be shutting down; re-run later for the EditMode suite.
    ) else (
      "%EDITOR%" -batchmode -quit -projectPath "%PROJ%" -runTests -testPlatform EditMode -testResults "%STATE%\editmode-results.xml" -logFile "%STATE%\unity-editmode.log"
      echo   EditMode results: %STATE%\editmode-results.xml
    )
  )
)

echo.
if "%FAILED%"=="0" (echo RESULT: PASS ^(see individual steps above^)) else (echo RESULT: FAILED ^(see steps above^))
popd
endlocal
