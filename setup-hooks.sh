#!/bin/bash
# Setup Git hooks for Linux/macOS

echo "Setting up Git hooks..."

# Configure Git to use custom hooks directory
git config core.hooksPath .githooks

# Make hooks executable
chmod +x .githooks/commit-msg
chmod +x .githooks/pre-commit

echo "✓ commit-msg hook configured"
echo "✓ pre-commit hook configured"
echo ""
echo "Git hooks setup complete!"
echo ""
echo "功能说明："
echo "   1. pre-commit: 提交前自动检查代码格式"
echo "   2. commit-msg: 检查提交信息格式（中文）"
echo ""
echo "如果格式检查失败，请运行："
echo "   cd godot/scripts/lattice && dotnet format"
