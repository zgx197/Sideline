@echo off
REM Setup Git hooks for Windows

echo Setting up Git hooks...

REM Configure Git to use custom hooks directory
git config core.hooksPath .githooks

REM Check if hooks exist
set HOOK_OK=true

if not exist .githooks\commit-msg (
    echo ❌ commit-msg hook not found
    set HOOK_OK=false
)

if not exist .githooks\pre-commit (
    echo ❌ pre-commit hook not found
    set HOOK_OK=false
)

if "%HOOK_OK%"=="false" (
    exit /b 1
)

echo ✓ commit-msg hook configured
echo ✓ pre-commit hook configured
echo.
echo Git hooks setup complete!
echo.
echo 功能说明：
echo   1. pre-commit: 提交前自动检查代码格式
echo   2. commit-msg: 检查提交信息格式（中文）
echo.
echo 如果格式检查失败，请运行：
echo   cd godot/scripts/lattice ^&^& dotnet format
echo.
pause
