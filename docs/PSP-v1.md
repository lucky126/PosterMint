# PosterMint Slot Protocol (PSP) v1 — 最小可跑版

**目的**：定义 CC 类工具（Claude Code / Cursor / …）产出的模板 JSON 格式，让 PosterMint 系统能（1）存进数据库、（2）在 Taro 小程序前端 Canvas 里渲染、（3）被 AI 理解并做占位替换。

**核心思想**：一切可替换的东西 = 一个 slot；slot 由 `id` 绝对定位 + `path` 语义定位；渲染树是嵌套的 layer。

**版本**：v1（最小可跑）。v1 明确不做条件显示、动画、主题变量、批量操作声明——v2 再补。

---

## 1. 顶层结构

一个模板 = 一个 JSON 文件：

```json
{
  "schema": "PSP-v1",
  "id": "canteen-classic-4col",
  "name": "四栏餐饮菜单",
  "canvas": { "w": 2480, "h": 1748, "unit": "px" },
  "layers": [ /* 见 §2 */ ]
}
```

| 字段 | 类型 | 必填 | 说明 |
|---|---|---|---|
| `schema` | string | ✓ | 固定 `"PSP-v1"` |
| `id` | string | ✓ | 模板全局唯一 ID（kebab-case） |
| `name` | string | ✓ | 人读的模板名 |
| `canvas.w/h` | int | ✓ | 画布原始像素尺寸；小程序端按比例缩放 |
| `layers` | array | ✓ | 渲染树；数组顺序 = 从底到顶的 z-order |

## 2. Layer 类型（4 种）

### 2.1 `background`（背景，不可替换）

```json
{ "type": "background", "fill": "#8B4513" }
{ "type": "background", "image": "assets/wood.jpg" }
```

### 2.2 `text`（文本占位）

```json
{
  "type": "text",
  "id": "s_shop_name",
  "path": "shop.name",
  "bbox": [80, 40, 400, 80],
  "font": { "family":"HYWenHei-85W", "size":56, "weight":700, "color":"#F5E9C6" },
  "align": "left",
  "value": "餐厅名",
  "editable": true,
  "overflow": "shrink",
  "minSize": 12
}
```

- `bbox = [x, y, w, h]`，左上原点
- `align`：`left | center | right`
- `overflow`：`shrink`（默认，字号缩到 `minSize`）| `ellipsis` | `wrap`
- `editable: false` = 装饰文字，AI 不动
- 可选 `suffix` / `prefix`（如价格加"元/份"）

### 2.3 `image`（图片占位）

```json
{
  "type": "image",
  "id": "s_hero_photo",
  "path": "hero.image",
  "bbox": [0, 0, 400, 320],
  "fit": "cover",
  "radius": 16,
  "value": null,
  "editable": true
}
```

- `fit`：`cover`（默认）| `contain` | `fill`
- `value: null` = 渲染成"占位提示"（虚线框 + "点击上传"）
- `editable: false` = 装饰图（logo / 印章），`value` 写死为 URL

### 2.4 `group`（重复组，可嵌套）

```json
{
  "type": "group",
  "id": "g_sections",
  "path": "sections",
  "bbox": [80, 500, 2320, 1200],
  "layout": { "kind": "grid", "cols": 4, "rowH": 260, "gap": 24 },
  "itemTemplate": {
    "layers": [
      { "type":"text", "path":"title", "bbox":[0,0,560,60], "font":{"size":42,"weight":700}, "value":"分类" },
      { "type":"group","path":"dishes","bbox":[0,80,560,1120],
        "layout": {"kind":"grid","cols":3,"rowH":220,"gap":16},
        "itemTemplate": {
          "layers": [
            { "type":"image","path":"image","bbox":[0,0,150,150],"fit":"cover","radius":75,"value":null },
            { "type":"text", "path":"name", "bbox":[0,160,150,32],"font":{"size":24},"align":"center","value":"菜名" },
            { "type":"text", "path":"price","bbox":[0,196,150,28],"font":{"size":22},"align":"center","value":"0","suffix":"元/份" }
          ]
        }
      }
    ]
  },
  "items": [
    { "title": "大席小炒", "dishes": [ {"name":"小米辣爆炒牛蛙","price":48,"image":null} ] },
    { "title": "凉菜系列", "dishes": [] }
  ]
}
```

- `itemTemplate.layers` = 每个 item 的模板；`items` = 实际数据
- item 的 key 对应 sub-layer 的 `path` 末段
- group 可无限嵌套；上例 `sections > dishes` = 双层

## 3. AI 寻址

任何 `editable: true` 的 layer = 一个 slot。两种定位方式：

**绝对（推荐 AI 内部使用）**：
```
s_shop_name
```

**语义（人读友好）**：
```
shop.name
sections[title=凉菜系列].dishes[name~=酸菜鱼].price
```

`path` 语法：
- `.field` — 对象字段
- `[n]` — 数组下标
- `[key=value]` — 精确匹配
- `[key~=substr]` — 模糊匹配（用于 AI 从自然语言映射时的容错）

## 4. AI patch 输出协议

小程序端 AI 对话后，大模型必须输出：

```json
{
  "operations": [
    { "target": "s_shop_name", "op": "set", "value": "海底捞火锅" },
    { "target": "sections[title=凉菜系列].dishes[name~=酸菜鱼].price", "op": "set", "value": 48 }
  ],
  "reply": "已改。"
}
```

v1 只支持 `op: "set"`。v2 再补 `append` / `delete` / `shift`（数组增减、数值调整）。

## 5. 后端入库校验规则

PC 后台粘 JSON 提交时必须验证：
- 顶层字段齐（`schema` / `id` / `name` / `canvas` / `layers`）
- `schema === "PSP-v1"`
- 所有 `editable: true` 的 layer 必须有 `id` 且 `id` 在整个模板中唯一
- 所有 `path` 在同一层级唯一（`sections[0].title` 与 `sections[1].title` 允许，`sections.title` × 2 不允许）
- `bbox` 四个数值均非负，且 `x+w ≤ canvas.w`、`y+h ≤ canvas.h`

## 6. v1 明确不做（v2 补）

- `visibleWhen`（条件显示）
- 动画 / 时间轴
- 主题变量绑定（`color: "$theme.primary"`）
- 批量操作声明（AI 靠提示词理解就够）
- 外部数据源导入（Excel/CSV）

## 7. 最小完整示例（可直接跑）

一张"店名 + 一道招牌菜"的极简海报：

```json
{
  "schema": "PSP-v1",
  "id": "minimal-single-dish",
  "name": "单菜招牌（最小示例）",
  "canvas": { "w": 1080, "h": 1440, "unit": "px" },
  "layers": [
    { "type": "background", "fill": "#8B0000" },

    { "type": "text",
      "id": "s_shop_name", "path": "shop.name",
      "bbox": [60, 60, 960, 100],
      "font": { "size": 72, "weight": 700, "color": "#FFF3D9" },
      "align": "center", "value": "餐厅名", "editable": true, "overflow": "shrink" },

    { "type": "image",
      "id": "s_hero_photo", "path": "hero.image",
      "bbox": [140, 240, 800, 800],
      "fit": "cover", "radius": 24, "value": null, "editable": true },

    { "type": "text",
      "id": "s_hero_name", "path": "hero.name",
      "bbox": [60, 1080, 960, 80],
      "font": { "size": 56, "weight": 700, "color": "#FFF3D9" },
      "align": "center", "value": "招牌菜名", "editable": true, "overflow": "shrink" },

    { "type": "text",
      "id": "s_hero_price", "path": "hero.price",
      "bbox": [60, 1200, 960, 100],
      "font": { "size": 96, "weight": 800, "color": "#FFD700" },
      "align": "center", "value": "88", "prefix": "¥", "editable": true }
  ]
}
```

这张模板有 **4 个 slot**：店名、主图、菜名、价格。CC 工具产出后交给 PosterMint，AI 通过对话把 4 个占位填成商户实际内容。
