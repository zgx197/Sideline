#!/bin/bash

# Lattice 测试快速运行脚本 (Linux/macOS)
# 用法: ./test.sh [命令]

set -e

# 颜色
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# 标题
show_title() {
    echo ""
    echo -e "${CYAN}========================================${NC}"
    echo -e "${CYAN}  $1${NC}"
    echo -e "${CYAN}========================================${NC}"
    echo ""
}

# 快速测试
run_quick() {
    show_title "快速测试"
    dotnet test --verbosity minimal
}

# 详细测试
run_all() {
    show_title "完整测试"
    dotnet test --verbosity normal
}

# 超详细
run_detail() {
    show_title "详细测试"
    dotnet test --verbosity detailed
}

# 列出测试
run_list() {
    show_title "测试列表"
    dotnet test --list-tests
}

# 分类测试
run_math() {
    show_title "数学测试"
    dotnet test --filter "FullyQualifiedName~Math" --verbosity normal
}

run_determinism() {
    show_title "确定性测试"
    dotnet test --filter "FullyQualifiedName~Determinism" --verbosity normal
}

run_robustness() {
    show_title "健壮性测试"
    dotnet test --filter "FullyQualifiedName~Robustness" --verbosity normal
}

# 覆盖率
run_coverage() {
    show_title "覆盖率报告"
    dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=lcov /p:CoverletOutput=./coverage/lcov.info
    
    if command -v genhtml &> /dev/null; then
        echo -e "${CYAN}生成 HTML 报告...${NC}"
        genhtml -o coverage/report coverage/lcov.info
        echo -e "${GREEN}报告已生成: coverage/report/index.html${NC}"
    fi
}

# 帮助
show_help() {
    echo ""
    echo "Lattice 测试快速运行工具"
    echo ""
    echo "用法: ./test.sh [命令]"
    echo ""
    echo "可用命令:"
    echo "  quick   - 快速运行所有测试（默认）"
    echo "  all     - 完整运行并显示详细输出"
    echo "  detail  - 超详细输出（用于调试）"
    echo "  list    - 列出所有测试用例"
    echo "  math    - 仅运行数学测试"
    echo "  det     - 仅运行确定性测试"
    echo "  rob     - 仅运行健壮性测试"
    echo "  cov     - 生成覆盖率报告"
    echo "  help    - 显示此帮助"
    echo ""
    echo "示例:"
    echo "  ./test.sh quick"
    echo "  ./test.sh det"
    echo "  ./test.sh cov"
}

# 主逻辑
cd "$(dirname "$0")"

COMMAND=${1:-quick}

case "$COMMAND" in
    quick)
        run_quick
        ;;
    all)
        run_all
        ;;
    detail)
        run_detail
        ;;
    list)
        run_list
        ;;
    math)
        run_math
        ;;
    det)
        run_determinism
        ;;
    rob)
        run_robustness
        ;;
    cov)
        run_coverage
        ;;
    help|-h|--help)
        show_help
        ;;
    *)
        echo -e "${RED}未知命令: $COMMAND${NC}"
        show_help
        exit 1
        ;;
esac

echo ""
echo -e "${GREEN}[✓] 测试完成${NC}"
echo ""
