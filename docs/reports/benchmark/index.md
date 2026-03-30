---
layout: default
title: 性能基准
description: Sideline 开发报告中心中的性能基准稳定入口页。
---

<section class="hero" style="padding: 72px 20px;">
    <h1>性能基准</h1>
    <p class="hero-subtitle">稳定入口页已就绪，Benchmark 报告后续会接入 Pages 的可浏览落地页。</p>
    <div class="hero-buttons">
        <a href="{{ '/reports/' | relative_url }}" class="pixel-btn pixel-btn-secondary">返回开发报告中心</a>
        <a href="https://github.com/zgx197/Sideline/actions/workflows/benchmark-full.yml" target="_blank" class="pixel-btn pixel-btn-primary">查看 Full Benchmark</a>
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
                <span class="report-chip report-chip-pending">静态报告自动发布待接入</span>
            </div>
            <p>
                `Full Benchmark` workflow 已经能够生成 benchmark 结果与 artifact，
                但这些结果目前还没有自动同步成 GitHub Pages 下的可浏览页面。
            </p>
            <p>
                本页先作为长期稳定入口，避免首页直接指向尚未部署的 `/reports/benchmark/` 产物目录。
            </p>
            <div class="report-actions">
                <a href="https://github.com/zgx197/Sideline/actions/workflows/benchmark-full.yml" target="_blank" class="pixel-btn pixel-btn-primary">查看 Benchmark Workflow</a>
                <a href="{{ '/development/CIBoundaries/' | relative_url }}" class="pixel-btn pixel-btn-secondary">查看 CI 边界说明</a>
            </div>
        </div>

        <div class="pixel-card pixel-card-green">
            <h3 style="margin-top: 0;">后续会补上的内容</h3>
            <ul class="report-meta-list">
                <li>最新 benchmark 报告的固定 URL</li>
                <li>生成时间、提交 SHA 与 run 链接</li>
                <li>历史 benchmark 报告归档</li>
                <li>入口页上的摘要指标与状态卡片</li>
            </ul>
            <p class="report-note">
                当前先保证“入口稳定”，后面再把 artifact 转成站点内容，避免站点设计和自动化链路继续脱节。
            </p>
        </div>
    </div>
</section>

<section class="content-container" style="padding-top: 0;">
    <div class="pixel-card">
        <h3 style="margin-top: 0;">现阶段怎么看 benchmark 数据</h3>
        <p>
            在 Pages 侧的静态报告接入之前，建议直接查看 GitHub Actions 中 `Full Benchmark`
            的运行结果和上传的 artifact。等自动化接好以后，本页会改为展示最新报告入口与摘要。
        </p>
        <div class="report-actions">
            <a href="https://github.com/zgx197/Sideline/actions" target="_blank" class="pixel-btn pixel-btn-secondary">打开 Actions 列表</a>
        </div>
    </div>
</section>
