---
layout: default
title: 代码覆盖率
description: Sideline 开发报告中心中的代码覆盖率稳定入口页。
---

<section class="hero" style="padding: 72px 20px;">
    <h1>代码覆盖率</h1>
    <p class="hero-subtitle">稳定入口页已就绪，自动发布的 HTML 覆盖率报告将在后续接入。</p>
    <div class="hero-buttons">
        <a href="{{ '/reports/' | relative_url }}" class="pixel-btn pixel-btn-secondary">返回开发报告中心</a>
        <a href="https://github.com/zgx197/Sideline/actions/workflows/ci-lattice.yml" target="_blank" class="pixel-btn pixel-btn-primary">查看 Lattice CI</a>
    </div>
</section>

<section class="content-container">
    <div class="report-split">
        <div class="pixel-card">
            <div class="report-card-title">
                <h3>当前状态</h3>
            </div>
            <div class="report-status-line">
                <span class="report-chip report-chip-live">入口稳定可访问</span>
                <span class="report-chip report-chip-pending">HTML 报告自动发布待接入</span>
            </div>
            <p>
                这个页面现在承担“稳定门户页”的职责：它永远存在，因此首页和导航不会再跳到 404。
                真正的覆盖率静态报告接入后，会挂载到这个入口页下面，而不是让首页直接指向一次性产物路径。
            </p>
            <p>
                在自动发布接入之前，覆盖率相关信息仍以 GitHub Actions、测试工程和后续 CI 产物为准。
            </p>
            <div class="report-actions">
                <a href="https://github.com/zgx197/Sideline/actions" target="_blank" class="pixel-btn pixel-btn-primary">查看 GitHub Actions</a>
                <a href="{{ '/development/CIArchitecture/' | relative_url }}" class="pixel-btn pixel-btn-secondary">查看 CI 架构说明</a>
            </div>
        </div>

        <div class="pixel-card pixel-card-green">
            <h3 style="margin-top: 0;">后续会补上的内容</h3>
            <ul class="report-meta-list">
                <li>最新覆盖率报告的可浏览页面</li>
                <li>最近一次生成时间与对应提交</li>
                <li>报告生成来源的 workflow / run 链接</li>
                <li>历史覆盖率报告归档入口</li>
            </ul>
            <p class="report-note">
                页面先稳定，自动化再补齐。这能避免官网对外承诺了入口，却没有真实落地页的情况。
            </p>
        </div>
    </div>
</section>

<section class="content-container" style="padding-top: 0;">
    <div class="pixel-card">
        <h3 style="margin-top: 0;">为什么现在不直接展示 HTML 覆盖率</h3>
        <p>
            当前仓库还没有“生成覆盖率静态站点并同步到 GitHub Pages”的完整自动化链路。
            因此本页暂时作为稳定入口，后面再接入真正的覆盖率报告内容。
        </p>
        <p>
            这样做的好处是：站点结构先固定下来，自动化落地时只需要把内容接进来，而不需要再次重构首页和导航。
        </p>
    </div>
</section>
