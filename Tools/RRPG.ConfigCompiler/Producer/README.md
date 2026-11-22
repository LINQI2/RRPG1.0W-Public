# Producer 入口（V0.1）

制作人**只需要**记住仓库根的三个 `.cmd`（双击即可）：

| 入口 | 作用 | 需要 Unity 关闭吗 |
|---|---|---|
| `RRPG_打开配置表.cmd` | 打开 `Development/Config/`（放工作簿的地方） | 不需要 |
| `RRPG_检查配置.cmd` | 只检查（`validate`，**不写任何文件**） | 不需要（Unity 开着也行） |
| `RRPG_一键导表.cmd` | 检查 → 生成 Canonical Mirror → Unity 生成资产 → 再验证 | **需要关闭** |

> 执行前先核对[当前工作簿职责与dirty范围](../../../Development/Config/README.md#当前工作簿与职责)。一键导表是全配置操作，不意味着自动允许把制作人未提交内容纳入其它任务。原工程TMP已有workspace例外；涉及运行验证继续使用隔离工程，不能用HEAD覆盖或猜测恢复。

## 内部结构（不是人工入口）

```text
RRPG_*.cmd                    ← 唯一人工入口（仓库根）
  └─ rrpg-config (Compiler)   ← Tools/RRPG.ConfigCompiler（validate / build）
  └─ Unity batchmode Builder  ← Assets/Editor/StaticConfig/StaticConfigBuilder.cs
```

* 本目录（`Tools/RRPG.ConfigCompiler/Producer/`）只放**可选的本机配置**，不是必记入口；
* 失败时一律给出：**原因 + 文件/行 + 下一步**；默认不写半套产物。

## 本机配置（可选）

Unity/Tuanjie 编辑器路径解析顺序：

1. 环境变量 `RRPG_TUANJIE_EXE`；
2. 本目录的 `editor-path.local.txt`（一行，写编辑器 `.exe` 的绝对路径；**不入 Git**）；
3. 本机已登记的默认路径（若存在）。

> 该路径是**本机环境**信息，不属于架构契约：换机器时改 1/2 即可。

## 形态声明

`RRPG_*.cmd` 是 **Wave 0 V0.1 = Migration V0.1 Implementation Choice**，
**不是**永久 Producer UI Authority：未来可以替换为 Editor Window / GUI，
只要仍满足 D-019 的「单一 Build Action」原则。
