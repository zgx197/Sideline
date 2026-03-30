---
layout: default
title: 性能基准
description: Sideline 开发报告中心中的完整性能基准入口页。
---

{% assign benchmark = site.data.reports_status.benchmark %}

<section class="hero" style="padding: 72px 20px;">
    <h1>性能基准</h1>
    <p class="hero-subtitle">入口页保持稳定，完整 Benchmark 报告由周期性 workflow 自动发布。</p>
    <div class="hero-buttons">
        <a href="{{ '/reports/' | relative_url }}" class="pixel-btn pixel-btn-secondary">返回开发报告中心</a>
        <a href="{{ benchmark.workflow_url }}" target="_blank" class="pixel-btn pixel-btn-primary">查看 Full Benchmark Workflow</a>
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
                {% if benchmark.available %}
                <span class="report-chip report-chip-ready">最新完整报告已发布</span>
                {% else %}
                <span class="report-chip report-chip-pending">等待首次完整报告</span>
                {% endif %}
            </div>
            <p>
                <code>[Analysis] [Weekly] Full Benchmark</code> 每周生成一次完整性能基准结果，
                <code>[Site] Reports Pages</code> 会把最近一次成功 artifact 挂载到本页下的 <code>latest/</code> 子路径。
            </p>
            {% if benchmark.available %}
            <ul class="report-meta-list">
                <li>最近生成时间：<code>{{ benchmark.generated_at }}</code></li>
                <li>对应分支：<code>{{ benchmark.branch }}</code></li>
                <li>对应提交：<code>{{ benchmark.commit | slice: 0, 8 }}</code></li>
            </ul>
            <div class="report-actions">
                <a href="{{ '/reports/benchmark/latest/' | relative_url }}" class="pixel-btn pixel-btn-primary">打开最新完整报告</a>
                <a href="{{ benchmark.run_url }}" target="_blank" class="pixel-btn pixel-btn-secondary">查看对应运行</a>
            </div>
            {% else %}
            <div class="report-actions">
                <a href="{{ benchmark.workflow_url }}" target="_blank" class="pixel-btn pixel-btn-primary">打开 Benchmark Workflow</a>
            </div>
            {% endif %}
        </div>

        <div class="pixel-card pixel-card-green">
            <h3 style="margin-top: 0;">为什么改成周期性完整 Benchmark</h3>
            <ul class="report-meta-list">
                <li>日常 PR 与主线验证只保留轻量 benchmark smoke，不再绑定完整基准测试。</li>
                <li>完整 benchmark 更适合作为固定环境下的趋势分析，而不是每次提交都刷新一次。</li>
                <li>站点展示读取最近一次成功结果，避免首页直接依赖临时 artifact 路径。</li>
            </ul>
            <p class="report-note">
                报告面向“性能回归分析”，不表示所有设备上的绝对性能，也不保证与当前 <code>main</code> 的最新提交完全同步。
            </p>
        </div>
    </div>
</section>
