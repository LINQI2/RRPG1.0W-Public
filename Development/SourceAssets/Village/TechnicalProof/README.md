# V16-D Sprite 技术证明样图

`farm-a.png` 是绿黄横纹 A；`farm-b.png` 是紫蓝竖纹 B，尺寸分别为 300×200、400×240。两图均为 **Technical Proof / Not Final Art**，不代表正式农田美术、视角或资源规格冻结。

同 ID 源图证明：把其中一图复制为 `../farm-proof.png`，保持 `va_village_farm_basic` 不变，然后运行正式 Compiler / Unity Builder。绑定证明：保持两张注册源图不动，把 `RRPG_VillagePresentation.xlsx / VillagePresentation` 中两个农田表现键的 `VisualAssetId` 改为 `va_village_farm_visual_b` 后重新导表。

交付状态为 A 图，两个农田键都绑定 `va_village_farm_basic`；另一个正式登记的源图 `../farm-proof-b.png` 保留 B。本目录只是可回放的输入样本，不是第二套 VisualAsset Authority。仅验证 PNG → Sprite，其它资源类型未验证。
