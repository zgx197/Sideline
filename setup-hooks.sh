#!/bin/bash
# Setup Git hooks for Linux/macOS

echo "Setting up Git hooks..."

# Configure Git to use custom hooks directory
git config core.hooksPath .githooks

# Make hooks executable
chmod +x .githooks/commit-msg

echo "✓ commit-msg hook configured and made executable"
echo ""
echo "Git hooks setup complete!"
echo ""
echo "Commit message format will be checked for:"
echo "  - Conventional commit format (recommended, not enforced)"
echo "  - Supports both Chinese and English"
echo ""
echo "Examples:"
echo "  feat(lattice): 添加 SIMD 批处理操作"
echo "  fix(ci): 修复 PowerShell 兼容性"
echo "  feat(lattice): add SIMD batch operations"
