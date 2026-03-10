---
layout: default
title: 首页
description: Sideline - 工作间隙偷偷经营地下世界的2D独立游戏
---

<!-- 英雄区域 -->
<section class="hero">
    <h1>SIDELINE</h1>
    <p class="hero-subtitle">工作间隙，偷偷经营你的地下世界</p>
    <div class="hero-buttons">
        <a href="#download" class="pixel-btn pixel-btn-primary">立即下载试玩</a>
        <a href="{{ '/about/' | relative_url }}" class="pixel-btn pixel-btn-secondary">了解更多</a>
        <a href="https://github.com/{{ site.github_username | default: 'username' }}/Sideline" class="pixel-btn pixel-btn-gold" target="_blank">⭐ Star on GitHub</a>
    </div>
    <p style="margin-top: 30px; color: var(--color-text-muted);">
        当前版本: <span class="tag tag-green">Phase 0 - 技术验证</span>
        预计 EA 发布: 2025 Q4
    </p>
</section>

<!-- 核心特性 -->
<section class="features-grid">
    <div class="pixel-card feature-item">
        <div class="feature-icon">🪟</div>
        <h3>双模式窗口</h3>
        <p>挂机模式：无边框小窗口，置顶显示，工作间隙也能关注你的地下世界。刷宝模式：全屏沉浸，深入地下城探险。</p>
    </div>
    
    <div class="pixel-card feature-item">
        <div class="feature-icon">⚔️</div>
        <h3>暗黑刷宝</h3>
        <p>Roguelike 地下城战斗，随机地图生成，丰富的装备系统，每次冒险都是全新体验。</p>
    </div>
    
    <div class="pixel-card feature-item">
        <div class="feature-icon">⏰</div>
        <h3>挂机养成</h3>
        <p>即使关闭游戏，你的地下世界仍在运转。离线收益系统让你的每一分钟都不被浪费。</p>
    </div>
    
    <div class="pixel-card feature-item">
        <div class="feature-icon">🎮</div>
        <h3>确定性联机</h3>
        <p>Phase 3 将加入 Lockstep 帧同步联机，基于自研 Lattice ECS 框架，与好友一起探索地下世界。</p>
    </div>
    
    <div class="pixel-card feature-item">
        <div class="feature-icon">⚙️</div>
        <h3>技术驱动</h3>
        <p>Godot 4 + C# 渲染层，自研 Lattice ECS 逻辑层，纯确定性计算支持战斗回放与帧同步。</p>
    </div>
    
    <div class="pixel-card feature-item">
        <div class="feature-icon">🎯</div>
        <h3>独立精神</h3>
        <p>个人开发者作品，专注核心玩法，拒绝商业化套路，为真正的游戏爱好者打造。</p>
    </div>
</section>

<!-- 下载区域 -->
<section id="download" class="content-container" style="text-align: center;">
    <h2>🎮 下载游戏</h2>
    
    <div class="pixel-card" style="max-width: 600px; margin: 0 auto;">
        <h3 style="margin-top: 0;">Phase 0 技术验证版</h3>
        <p>当前版本包含：无边框窗口原型、基础 UI 框架</p>
        <p style="color: var(--color-accent-gold);">即将推出：ECS 框架演示、FP 定点数验证</p>
        
        <div style="margin-top: 30px;">
            <a href="#" class="pixel-btn pixel-btn-primary" style="margin: 10px;">📥 Windows 版</a>
            <a href="https://store.steampowered.com" class="pixel-btn pixel-btn-secondary" style="margin: 10px;" target="_blank">🎮 Steam 愿望单</a>
        </div>
        
        <p style="margin-top: 20px; font-size: 0.9rem; color: var(--color-text-muted);">
            系统要求: Windows 10/11 | 需要 .NET 8.0 运行时
        </p>
    </div>
</section>

<!-- 最新动态 -->
<section class="content-container">
    <h2>📰 最新动态</h2>
    
    {% for post in site.posts limit:3 %}
    <div class="blog-item">
        <div class="blog-date">{{ post.date | date: "%Y年%m月%d日" }}</div>
        <a href="{{ post.url | relative_url }}" class="blog-title">{{ post.title }}</a>
        <p class="blog-excerpt">{{ post.excerpt | strip_html | truncate: 100 }}</p>
        {% for tag in post.tags %}
        <span class="tag">{{ tag }}</span>
        {% endfor %}
    </div>
    {% endfor %}
    
    {% if site.posts.size == 0 %}
    <div class="pixel-card" style="text-align: center; padding: 40px;">
        <p style="margin: 0; color: var(--color-text-muted);">开发日志即将发布，敬请期待...</p>
    </div>
    {% endif %}
    
    <div style="text-align: center; margin-top: 30px;">
        <a href="{{ '/blog/' | relative_url }}" class="pixel-btn pixel-btn-secondary">查看全部更新日志</a>
    </div>
</section>

<!-- 技术栈展示 -->
<section class="content-container">
    <h2>🛠️ 技术栈</h2>
    
    <table>
        <thead>
            <tr>
                <th>层级</th>
                <th>技术</th>
                <th>说明</th>
            </tr>
        </thead>
        <tbody>
            <tr>
                <td>渲染层</td>
                <td>Godot 4.6.1 + C#</td>
                <td>2D 俯视角渲染，GL Compatibility 模式</td>
            </tr>
            <tr>
                <td>逻辑层</td>
                <td>Lattice (自研)</td>
                <td>确定性 ECS 帧同步框架，纯 C#</td>
            </tr>
            <tr>
                <td>桥接层</td>
                <td>GodotRenderBridge</td>
                <td>同步 SimulationWorld 状态到 Godot Node</td>
            </tr>
            <tr>
                <td>网络层</td>
                <td>Steam Relay (计划中)</td>
                <td>Phase 3 实现 Lockstep 联机</td>
            </tr>
        </tbody>
    </table>
</section>
