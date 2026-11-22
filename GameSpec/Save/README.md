# Save 领域规则入口

> 状态：Current。Pre-8/30 Save Spec 已由当前 Save Domain 替代；历史 Save schema / rationale 通过 Git History 追溯。Legacy DTO compatibility 不获得 Current Rule Authority。

[Save-GameSpec-V0.1.md](Save-GameSpec-V0.1.md) 定义已确认的持久化契约与明确的非承诺边界。Save 负责跨进程持久化的通用身份、版本与序列化边界，不重复定义 Person、Village 或 Expedition 产品状态；各领域状态的含义和所有权仍服从对应 [Person](../Person/README.md)、[Village](../Village/Village-GameSpec-V0.1.md)、[Expedition](../Expedition/Expedition-Progression-GameSpec-V0.1.md) Current GameSpec。Battle 单场运行时不是长期 Save Authority。

Current 实现事实应另行核对 [技术架构总览](../../Docs/Architecture/技术架构总览.md)、[项目状态快照](../../Docs/项目状态快照.md)与代码。Legacy DTO 字段及兼容读取不自动成为 Current 产品规则；保存触发 Ownership 仍待技术决定，见 [问题 40](../../Docs/问题清单.md)。
