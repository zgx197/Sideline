---
layout: default
title: 开发报告
description: Sideline 开发报告中心，汇总完整覆盖率、完整性能基准与自动化报告入口。
---

{% assign coverage = site.data.reports_status.coverage %}
{% assign benchmark = site.data.reports_status.benchmark %}

<section class="hero" style="padding: 72px 20px;">
    <h1>开发报告</h1>
    <p class="hero-subtitle">稳定入口先落地，完整分析报告由周期性 workflow 自动接入。</p>
    <div class="hero-buttons">
        <a href="{{ '/reports/coverage/' | relative_url }}" class="pixel-btn pixel-btn-primary">代码覆盖率入口</a>
        <a href="{{ '/reports/benchmark/' | relative_url }}" class="pixel-btn pixel-btn-secondary">性能基准入口</a>
        <a href="https://github.com/zgx197/Sideline/actions" target="_blank" class="pixel-btn pixel-btn-gold">查看 GitHub Actions</a>
    </div>
</section>

<section class="report-grid">
    <a href="{{ '/reports/coverage/' | relative_url }}" class="pixel-card feature-item" style="text-decoration: none; color: inherit; display: block;">
        <div class="feature-icon">📈</div>
        <h3>代码覆盖率</h3>
        <p>完整覆盖率报告由 <code>[Analysis] [Weekly] Full Coverage</code> 周期性生成，并由 <code>[Site] Reports Pages</code> 自动发布到站点。</p>
        <div style="margin-top: 15px;">
            <span class="tag tag-green">入口稳定</span>
            {% if coverage.available %}
            <span class="tag">最新完整报告已发布</span>
            {% else %}
            <span class="tag">等待首次完整报告</span>
            {% endif %}
        </div>
    </a>

    <a href="{{ '/reports/benchmark/' | relative_url }}" class="pixel-card feature-item" style="text-decoration: none; color: inherit; display: block;">
        <div class="feature-icon">⚡</div>
        <h3>性能基准</h3>
        <p>完整性能基准由 <code>[Analysis] [Weekly] Full Benchmark</code> 周期性生成，并由 <code>[Site] Reports Pages</code> 自动同步到网站。</p>
        <div style="margin-top: 15px;">
            <span class="tag tag-green">入口稳定</span>
            {% if benchmark.available %}
            <span class="tag">最新完整报告已发布</span>
            {% else %}
            <span class="tag">等待首次完整报告</span>
            {% endif %}
        </div>
    </a>
</section>

<section class="content-container" style="max-width: 900px; margin: 40px auto;">
    <div class="pixel-card">
        <h3 style="margin-top: 0;">信息架构说明</h3>
        <p>开发报告区现在明确拆成两层：</p>
        <ul style="line-height: 1.8;">
            <li><strong>稳定入口页</strong>：始终存在，不再把首页直接指向一次性 artifact 路径。</li>
            <li><strong>完整报告内容</strong>：由周期性分析 workflow 生成，并在发布时挂载到入口页下的 <code>latest/</code> 子路径。</li>
        </ul>
        <p class="report-note" style="margin-top: 20px;">
            这样即使某一次定时任务还没产出新报告，网站仍然有可访问的解释页面，不会再出现入口直接跳到 404 的情况。
        </p>
    </div>
</section>
