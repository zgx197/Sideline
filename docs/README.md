# Sideline 官方网站

这是 **Sideline** 游戏的官方网站，使用 Jekyll 和 GitHub Pages 构建。

## 网站结构

```
docs/
├── _config.yml          # Jekyll 配置文件
├── _layouts/            # 布局模板
│   └── default.html     # 默认布局
├── _posts/              # 博客文章
│   └── 2026-03-11-website-launch.md
├── _includes/           # 可复用组件
├── assets/              # 静态资源
│   ├── css/
│   │   └── pixel-style.css  # 像素风格样式
│   └── images/
├── index.md             # 首页
├── about.md             # 关于游戏
├── docs.md              # 开发文档
└── blog.md              # 更新日志
```

## 本地开发

### 前提条件

- Ruby 2.5+
- Bundler

### 安装依赖

```bash
cd docs
bundle install
```

### 启动本地服务器

```bash
bundle exec jekyll serve --config _config.local.yml
```

访问 http://localhost:4000/Sideline/

本地预览使用完整的 `_config.local.yml`，避免因为 `remote_theme` 下载远程主题而受到本机代理或证书链问题影响。

## 添加新文章

在 `_posts/` 目录下创建新文件，命名格式：`YYYY-MM-DD-title.md`

```yaml
---
layout: default
title: "文章标题"
date: 2026-03-11 12:00:00 +0800
categories: 分类
tags: [标签1, 标签2]
---

文章内容...
```

## 部署

网站会自动部署到 GitHub Pages：

1. 推送代码到 GitHub
2. 在仓库 Settings → Pages 中选择部署分支
3. 等待几分钟，网站即可访问

访问地址：`https://<username>.github.io/Sideline/`
