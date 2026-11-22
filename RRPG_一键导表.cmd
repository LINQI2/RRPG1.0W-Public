@echo off
rem ============================================================================
rem  RRPG Static Configuration - Producer entry 3/3 : One-click build
rem    XLSX  ->  Compiler build (Canonical Mirror)
rem          ->  Unity Builder (Generated Root, not in Git)
rem          ->  verify (validate)
rem  Requires: Unity editor CLOSED for this project. One action, no inner steps.
rem  ASCII-only on purpose: Chinese guidance is printed by the tools themselves.
rem ============================================================================
chcp 65001 >nul
setlocal
set "REPO=%~dp0"
set "REPO1=%REPO:~0,-1%"
set "CFG=%REPO%Tools\RRPG.ConfigCompiler"
set "EXE=%CFG%\src\RRPG.ConfigCompiler\bin\Release\net10.0\rrpg-config.exe"
set "PROJ=%REPO%UnityProject\RRPG1.0game"
set "STAGE=%CFG%\.build-state\builder.stage.txt"
set "EDITORLOG=%CFG%\.build-state\unity-builder.log"

echo.
echo === RRPG Static Configuration : one-click build ===
echo.

where dotnet >nul 2>&1
if errorlevel 1 (
  echo [BLOCKED] .NET SDK ^(dotnet^) not found on PATH.
  goto :end
)

if exist "%PROJ%\Temp\UnityLockfile" (
  echo [BLOCKED] The Unity editor is open for this project.
  echo           Close the Tuanjie/Unity editor window, then run this entry again.
  goto :end
)

if not exist "%EXE%" (
  echo [1/3] Building the config compiler ^(first run may restore NuGet packages^) ...
  dotnet build "%CFG%\src\RRPG.ConfigCompiler\RRPG.ConfigCompiler.csproj" -c Release -v q
  if errorlevel 1 (
    echo [FAILED] compiler build failed.
    goto :end
  )
)

echo [1/3] Compiler build ^(XLSX -^> Canonical Mirror^) ...
"%EXE%" build --root "%REPO1%"
if errorlevel 1 (
  echo [FAILED] compiler build failed. The mirror was NOT updated.
  goto :end
)

echo.
echo [2/3] Unity Builder ^(Generated Root; not in Git^) ...
set "EDITOR="
if defined RRPG_TUANJIE_EXE set "EDITOR=%RRPG_TUANJIE_EXE%"
if not defined EDITOR if exist "%CFG%\Producer\editor-path.local.txt" (
  for /f "usebackq delims=" %%E in ("%CFG%\Producer\editor-path.local.txt") do set "EDITOR=%%E"
)
if not defined EDITOR if exist "D:\BBB\Tuanjie\Hub\Editor\2022.3.62t9\Editor\Tuanjie.exe" set "EDITOR=D:\BBB\Tuanjie\Hub\Editor\2022.3.62t9\Editor\Tuanjie.exe"

if not defined EDITOR (
  echo [BLOCKED] Unity editor executable not found.
  echo           Set the environment variable RRPG_TUANJIE_EXE to the editor .exe path,
  echo           or write that path into Tools\RRPG.ConfigCompiler\Producer\editor-path.local.txt
  goto :end
)

if not exist "%CFG%\.build-state" mkdir "%CFG%\.build-state"
if exist "%STAGE%" del /f /q "%STAGE%"

"%EDITOR%" -batchmode -quit -projectPath "%PROJ%" -executeMethod RRPG.StaticConfig.EditorTools.StaticConfigBuilder.ExecuteBuild -logFile "%EDITORLOG%"
if errorlevel 1 (
  echo [WARN] Unity batchmode returned non-zero; checking the stage file ...
)

if not exist "%STAGE%" (
  echo [FAILED] no builder stage file was produced. Unity log: %EDITORLOG%
  goto :end
)

findstr /l /c:"result=PASS" "%STAGE%" >nul
if errorlevel 1 (
  echo [FAILED] Unity Builder reported a failure:
  type "%STAGE%"
  echo   Unity log: %EDITORLOG%
  goto :end
)
echo   Unity Builder: PASS
findstr /l /c:"noOp=true" "%STAGE%" >nul
if not errorlevel 1 echo   ^(no changes: the generated root already matched the mirror^)

echo.
echo [3/3] Verify ^(validate^) ...
"%EXE%" validate --root "%REPO1%"
if errorlevel 1 (
  echo [FAILED] verification failed after the build. Nothing else was touched.
  goto :end
)

echo.
echo RESULT: PASS - configuration built and verified.
echo.

:end
pause
exit /b 0
