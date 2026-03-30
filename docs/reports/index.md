---
layout: default
title: 开发报告
description: Sideline 开发报告中心，汇总覆盖率、性能基准与自动化状态入口。
---

<section class="hero" style="padding: 72px 20px;">
    <h1>开发报告</h1>
    <p class="hero-subtitle">稳定入口先落地，自动化报告内容逐步接入。</p>
    <div class="hero-buttons">
        <a href="{{ '/reports/coverage/' | relative_url }}" class="pixel-btn pixel-btn-primary">代码覆盖率入口</a>
        <a href="{{ '/reports/benchmark/' | relative_url }}" class="pixel-btn pixel-btn-secondary">性能基准入口</a>
        <a href="https://github.com/zgx197/Sideline/actions" target="_blank" class="pixel-btn pixel-btn-gold">查看 GitHub Actions</a>
    </div>
</section>

<section class="report-grid">
    <a href="{{ '/reports/coverage/' | relative_url }}" class="pixel-card feature-item" style="text-decoration: none; color: inherit; display: block;">
        <div class="feature-icon">🎯</div>
        <h3>代码覆盖率</h3>
        <p>稳定入口已经就绪。后续会在这个页面下接入可浏览的覆盖率 HTML 报告、生成时间与对应 run 信息。</p>
        <div style="margin-top: 15px;">
            <span class="tag tag-green">入口稳定</span>
            <span class="tag">自动发布待接入</span>
        </div>
    </a>

    <a href="{{ '/reports/benchmark/' | relative_url }}" class="pixel-card feature-item" style="text-decoration: none; color: inherit; display: block;">
        <div class="feature-icon">⚡</div>
        <h3>性能基准</h3>
        <p>Benchmark 入口页已经稳定存在。当前先承接说明与状态信息，后续再接入自动发布的静态报告内容。</p>
        <div style="margin-top: 15px;">
            <span class="tag tag-green">入口稳定</span>
            <span class="tag">报告内容待接入</span>
        </div>
    </a>
</section>

<section class="content-container" style="max-width: 800px; margin: 40px auto;">
    <div class="pixel-card">
        <h3 style="margin-top: 0;">关于当前的信息架构</h3>
        <p>开发报告区现在优先保证“页面稳定存在”，不再让首页直接链接到还没有部署出来的临时产物目录。</p>
        <ul style="line-height: 1.8;">
            <li><strong>代码覆盖率</strong>：先提供稳定入口页，后续接入 HTML 覆盖率报告。</li>
            <li><strong>性能基准</strong>：先提供稳定入口页，后续接入 Benchmark 静态报告与摘要。</li>
            <li><strong>自动化状态</strong>：在报告内容接入前，统一回落到 GitHub Actions 作为权威来源。</li>
        </ul>
        <p style="margin-top: 20px;" class="report-note">
            这样即使某次自动化还没有产出站点内容，官网也只会显示状态说明，不会再直接把用户带到 404 页面。
        </p>
    </div>
</section>

<section class="content-container" style="max-width: 800px; margin: 40px auto; text-align: center;">
    <h2>CI 构建状态</h2>
    <div style="margin-top: 20px;">
        <a href="https://github.com/zgx197/Sideline/actions" target="_blank" class="pixel-btn pixel-btn-secondary">
            查看 GitHub Actions 运行状态
        </a>
    </div>
</section>
