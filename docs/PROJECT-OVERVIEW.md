# Project Overview

`PosterMint` 是独立于旧版 `catering-agent` 的新 .NET 项目，用来承接“商场端 AI 制模板、商铺端只用模板”的正式实现。

当前文档分工：

1. [FINAL-DESIGN.md](./FINAL-DESIGN.md)
   作用：当前 .NET 项目的设计基线，明确业务边界、角色权限、AI 分工、模板协议、数据库和 API。
2. [IMPLEMENTATION-ROADMAP.md](./IMPLEMENTATION-ROADMAP.md)
   作用：把设计基线拆成实现顺序，标出当前已完成、部分完成和未完成的能力。
3. [PHASE1-CATERING-PLAN.md](./PHASE1-CATERING-PLAN.md)
   作用：客户「一期·餐饮」目标（小程序 + Web-PC 后台）的落地设计与实施计划，含差距分析、架构、数据模型、API 切片、里程碑与待决策点。

当前代码现状：

1. 已有场景式入口页、对话式编辑页、模板后备入口页。
2. 已有 SQLite、模板种子数据、会话、HTML 预览、基础对话式内容修改能力。
3. 尚未完成商场后台模板生产、审核授权、正式权限体系、PNG 导出、标签推荐和参考图逆向工程。

使用建议：

1. 先以 `FINAL-DESIGN.md` 统一产品和技术边界。
2. 再按 `IMPLEMENTATION-ROADMAP.md` 的 P0 / P1 / P2 顺序推进。
