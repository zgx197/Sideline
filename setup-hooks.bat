@echo off
REM Setup Git hooks for Windows

echo Setting up Git hooks...

REM Configure Git to use custom hooks directory
git config core.hooksPath .githooks

REM Make hook executable (on Windows, this just ensures the file exists)
if exist .githooks\commit-msg (
    echo ✓ commit-msg hook configured
) else (
    echo ❌ commit-msg hook not found
    exit /b 1
)

echo.
echo Git hooks setup complete!
echo.
echo Commit message format will be checked for:
echo   - Conventional commit format (recommended, not enforced)
echo   - Supports both Chinese and English
echo.
echo Examples:
echo   feat(lattice): 添加 SIMD 批处理操作
echo   fix(ci): 修复 PowerShell 兼容性
echo   feat(lattice): add SIMD batch operations
echo.
pause
