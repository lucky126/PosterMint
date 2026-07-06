# PosterMint

这是一个独立于 `catering-agent` 的全新 .NET 8 骨架项目，路径位于：

```text
O:\test\projects\posters\PosterMint
```

目标：

1. 用 .NET 8 分层结构承接餐饮海报系统重构。
2. 提供最小可运行的 Web、Worker、SQLite、模板、会话、配置和健康检查骨架。
3. 不修改原有 `catering-agent` 项目。

## 解决方案结构

```text
PosterMint.slnx
src/
  PosterMint.Web
  PosterMint.Application
  PosterMint.Domain
  PosterMint.Infrastructure
  PosterMint.Worker
tests/
  PosterMint.UnitTests
  PosterMint.IntegrationTests
docs/
```

## 启动

### Web

```powershell
cd O:\test\projects\posters\PosterMint
dotnet run --project .\src\PosterMint.Web
```

### Worker

```powershell
dotnet run --project .\src\PosterMint.Worker
```

## 当前已实现

1. SQLite `DbContext` 与基础种子数据。
2. 模板列表、模板详情、创建模板 API。
3. 商铺会话创建、查询 API。
4. 配置查询 API。
5. 存活与就绪健康检查。
6. 推荐打分单元测试。
7. 健康检查集成测试。
