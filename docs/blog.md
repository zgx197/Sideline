---
layout: default
title: 更新日志
description: Sideline 开发日志、版本更新和里程碑记录
---

<section class="hero">
    <h1>更新日志</h1>
    <p class="hero-subtitle">开发历程与版本更新</p>
</section>

<div class="content-container">

<div class="blog-list">

{% if site.posts.size > 0 %}

{% for post in site.posts %}
<div class="blog-item">
    <div class="blog-date">{{ post.date | date: "%Y年%m月%d日" }}</div>
    <a href="{{ post.url | relative_url }}" class="blog-title">{{ post.title }}</a>
    <p class="blog-excerpt">{{ post.excerpt | strip_html | truncate: 150 }}</p>
    {% for tag in post.tags %}
    <span class="tag">{{ tag }}</span>
    {% endfor %}
</div>
{% endfor %}

{% else %}

<!-- 示例文章，当没有真实文章时显示 -->
<div class="blog-item">
    <div class="blog-date">2026年3月11日</div>
    <span class="blog-title">🎉 项目网站上线</span>
    <p class="blog-excerpt">
        Sideline 官方网站正式上线！网站使用 Jekyll + GitHub Pages 构建，采用像素游戏风格设计，
        包含游戏介绍、开发文档和更新日志等模块。
    </p>
    <span class="tag">公告</span>
    <span class="tag">网站</span>
</div>

<div class="blog-item">
    <div class="blog-date">2026年3月10日</div>
    <span class="blog-title">🪟 无边框窗口原型完成</span>
    <p class="blog-excerpt">
        成功实现了挂机模式的无边框窗口原型。窗口可以置顶显示、自由拖拽，
        按 ESC 键可在挂机模式和刷宝模式之间切换。这是 Phase 0 的第一个重要里程碑。
    </p>
    <span class="tag tag-green">技术验证</span>
    <span class="tag">Godot</span>
</div>

<div class="blog-item">
    <div class="blog-date">2026年3月1日</div>
    <span class="blog-title">🚀 Sideline 项目启动</span>
    <p class="blog-excerpt">
        项目正式启动！确定了"工作间隙偷偷经营地下世界"的核心创意，
        选定 Godot 4 + C# 作为技术栈，开始编写基础框架代码。
    </p>
    <span class="tag">里程碑</span>
</div>

{% endif %}

</div>

---

## 📅 里程碑

<div class="pixel-card" style="margin-top: 40px;">
    <h3 style="margin-top: 0;">开发路线图</h3>
    
    <table style="margin: 0;">
        <thead>
            <tr>
                <th>阶段</th>
                <th>目标</th>
                <th>状态</th>
            </tr>
        </thead>
        <tbody>
            <tr>
                <td><strong>Phase 0</strong></td>
                <td>技术验证 - 无边框窗口、ECS 框架、定点数</td>
                <td><span class="tag tag-green">进行中</span></td>
            </tr>
            <tr>
                <td><strong>Phase 1</strong></td>
                <td>核心玩法 - 地图生成、战斗系统、装备系统</td>
                <td><span class="tag">计划中</span></td>
            </tr>
            <tr>
                <td><strong>Phase 2</strong></td>
                <td>EA 发布准备 - Steam 接入、内容填充</td>
                <td><span class="tag">计划中</span></td>
            </tr>
            <tr>
                <td><strong>Phase 3</strong></td>
                <td>联机 DLC - Lockstep 帧同步、多人模式</td>
                <td><span class="tag">计划中</span></td>
            </tr>
        </tbody>
    </table>
</div>

---

## 📬 订阅更新

<p>想要第一时间获取开发进展？关注我们的 GitHub 仓库或加入 Steam 愿望单！</p>

<div style="text-align: center; margin-top: 30px;">
    <a href="https://github.com/username/Sideline" class="pixel-btn pixel-btn-secondary" target="_blank">GitHub</a>
    <a href="https://store.steampowered.com" class="pixel-btn pixel-btn-primary" target="_blank" style="margin-left: 20px;">Steam 愿望单</a>
</div>

</div>
