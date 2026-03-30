---
layout: default
title: 代码覆盖率
description: Sideline 开发报告中心中的完整代码覆盖率入口页。
---

{% assign coverage = site.data.reports_status.coverage %}

<section class="hero" style="padding: 72px 20px;">
    <h1>代码覆盖率</h1>
    <p class="hero-subtitle">入口页保持稳定，完整 HTML 报告由周期性 workflow 自动发布。</p>
    <div class="hero-buttons">
        <a href="{{ '/reports/' | relative_url }}" class="pixel-btn pixel-btn-secondary">返回开发报告中心</a>
        <a href="{{ coverage.workflow_url }}" target="_blank" class="pixel-btn pixel-btn-primary">查看 Full Coverage Workflow</a>
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
                {% if coverage.available %}
                <span class="report-chip report-chip-ready">最新完整报告已发布</span>
                {% else %}
                <span class="report-chip report-chip-pending">等待首次完整报告</span>
                {% endif %}
            </div>
            <p>
                <code>[Analysis] [Weekly] Full Coverage</code> 每周生成一次完整覆盖率 HTML 报告，
                <code>[Site] Reports Pages</code> 会把最近一次成功结果挂载到本页下的 <code>latest/</code> 子路径。
            </p>
            {% if coverage.available %}
            <ul class="report-meta-list">
                <li>最近生成时间：<code>{{ coverage.generated_at }}</code></li>
                <li>对应分支：<code>{{ coverage.branch }}</code></li>
                <li>对应提交：<code>{{ coverage.commit | slice: 0, 8 }}</code></li>
            </ul>
            <div class="report-actions">
                <a href="{{ '/reports/coverage/latest/' | relative_url }}" class="pixel-btn pixel-btn-primary">打开最新完整报告</a>
                <a href="{{ coverage.run_url }}" target="_blank" class="pixel-btn pixel-btn-secondary">查看对应运行</a>
            </div>
            {% else %}
            <div class="report-actions">
                <a href="{{ coverage.workflow_url }}" target="_blank" class="pixel-btn pixel-btn-primary">打开 Coverage Workflow</a>
            </div>
            {% endif %}
        </div>

        <div class="pixel-card pixel-card-green">
            <h3 style="margin-top: 0;">为什么改成周期性完整覆盖率</h3>
            <ul class="report-meta-list">
                <li>主线验证回归到日常门禁，不再为完整覆盖率报告额外拉长耗时。</li>
                <li>覆盖率报告与性能基准统一归入 analysis 层，职责边界更清晰。</li>
                <li>站点展示直接读取最近一次成功的完整报告，而不是依赖日常提交即时产出。</li>
            </ul>
            <p class="report-note">
                站点上展示的是“最近一次完整分析结果”，不保证与当前 <code>main</code> 的最新提交完全同步。
            </p>
        </div>
    </div>
</section>
