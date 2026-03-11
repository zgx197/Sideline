---
layout: default
title: 开发报告
description: Lattice 框架代码覆盖率与性能基准测试报告
---

<!-- 开发报告首页 -->
<section class="hero" style="padding: 60px 20px;">
    <h1>📊 开发报告</h1>
    <p class="hero-subtitle">Lattice 框架代码质量与性能监控</p>
</section>

<!-- 报告卡片 -->
<section class="features-grid" style="max-width: 900px; margin: 0 auto;">
    
    <!-- 覆盖率报告 -->
    <a href="{{ '/reports/coverage/' | relative_url }}" class="pixel-card feature-item" style="text-decoration: none; color: inherit; display: block;">
        <div class="feature-icon">🎯</div>
        <h3>代码覆盖率</h3>
        <p>查看 Lattice 框架的单元测试覆盖情况，包括 FP 定点数、ECS 核心等模块的测试覆盖度。</p>
        <div style="margin-top: 15px;">
            <span class="tag tag-green">实时更新</span>
            <span class="tag">当前: 74%</span>
        </div>
    </a>
    
    <!-- 性能基准 -->
    <a href="{{ '/reports/benchmark/' | relative_url }}" class="pixel-card feature-item" style="text-decoration: none; color: inherit; display: block;">
        <div class="feature-icon">⚡</div>
        <h3>性能基准</h3>
        <p>对比 FP 定点数与 float 的性能表现，包括运算速度、内存分配和批量处理测试。</p>
        <div style="margin-top: 15px;">
            <span class="tag tag-green">自动测试</span>
            <span class="tag">多平台</span>
        </div>
    </a>
    
</section>

<!-- 说明 -->
<section class="content-container" style="max-width: 800px; margin: 40px auto;">
    <div class="pixel-card">
        <h3 style="margin-top: 0;">📋 关于开发报告</h3>
        <p>本页面展示 Sideline 项目自研 Lattice ECS 框架的自动化测试报告，包括：</p>
        <ul style="line-height: 1.8;">
            <li><strong>代码覆盖率</strong>：通过 170+ 单元测试验证框架稳定性</li>
            <li><strong>性能基准</strong>：定点数运算性能对比与回归测试</li>
            <li><strong>确定性验证</strong>：跨平台帧同步一致性检测</li>
        </ul>
        <p style="margin-top: 20px; color: var(--color-text-muted);">
            报告由 GitHub Actions 自动生成并部署，每次代码提交后自动更新。
        </p>
    </div>
</section>

<!-- CI 状态 -->
<section class="content-container" style="max-width: 800px; margin: 40px auto; text-align: center;">
    <h2>🔧 CI 构建状态</h2>
    <div style="margin-top: 20px;">
        <a href="https://github.com/zgx197/Sideline/actions" target="_blank" class="pixel-btn pixel-btn-secondary">
            查看 GitHub Actions 运行状态
        </a>
    </div>
</section>
