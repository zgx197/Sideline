# Lattice 测试快速运行指南

## 快速开始

### Windows (PowerShell) - 推荐
```powershell
# 快速运行所有测试
.\run-tests.ps1

# 运行特定类别测试
.\run-tests.ps1 -Category "Determinism"
.\run-tests.ps1 -Category "Robustness"
.\run-tests.ps1 -Category "Math"

# 详细输出
.\run-tests.ps1 -Detailed

# 列出所有测试
.\run-tests.ps1 -List

# 生成覆盖率报告
.\run-tests.ps1 -Coverage
```

### Windows (CMD)
```cmd
# 快速测试
test.bat quick

# 详细测试
test.bat all

# 分类测试
test.bat det      :: 确定性测试
test.bat rob      :: 健壮性测试
test.bat math     :: 数学测试

# 列出所有测试
test.bat list
```

### Linux / macOS
```bash
# 添加执行权限
chmod +x test.sh

# 快速运行所有测试
./test.sh quick

# 运行特定类别
./test.sh det     # 确定性测试
./test.sh rob     # 健壮性测试
./test.sh math    # 数学测试

# 生成覆盖率报告
./test.sh cov
```

## 测试分类

| 类别 | 数量 | 说明 |
|------|------|------|
| **Math** | 40 | FP 基础运算测试 |
| **Determinism** | 28 | 确定性验证测试 |
| **Robustness** | 67 | 溢出、除零、边界测试 |
| **其他** | 35 | 线程安全、哈希、序列化等 |

## 常用命令速查

### 开发时快速检查
```powershell
.\run-tests.ps1                    # 30秒快速验证
```

### 提交前完整检查
```powershell
.\run-tests.ps1 -Detailed          # 详细输出
```

### 调试特定失败
```powershell
.\run-tests.ps1 -Detailed -Category "Robustness"
```

### 查找测试名称
```powershell
.\run-tests.ps1 -List | Select-String "Overflow"
```

## 在 VS Code 中运行

添加到 `.vscode/tasks.json`：

```json
{
    "label": "Run Tests",
    "type": "shell",
    "command": "powershell",
    "args": ["-File", "${workspaceFolder}/godot/Lattice.Tests/run-tests.ps1"],
    "group": {
        "kind": "test",
        "isDefault": true
    }
}
```

## CI/CD 集成

### GitHub Actions
```yaml
- name: Run Tests
  run: |
    cd godot/Lattice.Tests
    dotnet test --verbosity minimal
```

### 本地预提交钩子
```bash
# .git/hooks/pre-commit
#!/bin/bash
cd godot/Lattice.Tests || exit 1
dotnet test --verbosity minimal || exit 1
```

---

**总计: 170 个测试，覆盖 FP 定点数所有核心功能**
