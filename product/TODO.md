# RuinedHall TODO

## 性能优化（HelpOthers / 移动端）

> 目标：保持暗黑类「怪多」的体验，优先减 GPU/单帧 CPU，而不是简单砍怪物总数。  
> 分析日期：2026-09-07

### P0 — 优先做（收益最大）

- [ ] **Crest 海洋模拟降档（GPU）**  
  HelpOthers 场景 Ocean LOD 384 / 7 级；移动端改为 128–256、4–5 级，关闭泡沫/海底深度等不可见 sim。不可见区域改用静态水面。

- [ ] **Mobile URP 关闭多余 RT（GPU）**  
  `Mobile_RPAsset` 的 `RequireDepthTexture` / `RequireOpaqueTexture` 改为按需；确认 Crest/后处理依赖后再关。

- [ ] **血条改为统一管理（CPU/GPU）**  
  现状每个 `CharacterCombatAgent` 一个 `WorldHealthBar` Canvas（50+ Draw Call）。改为单一 HUD/图集 Billboard，>30m 隐藏。

- [x] **怪物 AI 分级（CPU，保留数量）**  
  不砍每营 7–10 只；按与玩家距离分档：  
  - **近（≤25m）**：完整 AI + 动画 + 碰撞  
  - **中（25–50m）**：巡逻/待机，降频 Update（每 3 帧）  
  - **远（>50m）**：Sleep/冻结姿态，停 PlayableGraph  
  已实现：`EnemySpawner` 营地激活 + `CharacterCombatAgent` 三级 LOD。

- [x] **区块/营地激活（暗黑式刷怪）**  
  玩家进入营地 42m 唤醒、离开 52m 休眠；刷怪数量不变。

### P1 — 次优先

- [ ] **NPC 动画剔除**  
  远距：`AnimatorCullingMode.CullUpdateTransforms`，`updateWhenOffscreen = false`；仅主角战斗段 `AlwaysAnimate`。  
  涉及：`HeroController`、`GameplayBootstrap`、`CharacterActionPlayer`、`SidekickWardrobe`。

- [ ] **Rooster 动画方案简化**  
  同种怪共用 `AnimatorController`，减少每只独立 `PlayableGraph`（`CharacterActionPlayer`）。

- [ ] **Sidekick 启动合网格预烘焙**  
  `GameplayBootstrap` 启动时 `SidekickWardrobe.CreatePlayable` 合网格改编辑期 prefab，运行时只 Instantiate。

- [ ] **WaterProbe 缓存与降频**  
  水体边界预计算；巡逻/移动时减少 `CrossesWater` 子采样；远距怪跳过水检测。

- [ ] **Mobile 渲染档位**  
  `renderScale` 0.75–0.85；阴影距离 25–35；低端关 HDR；相机 far clip 200–300。

- [ ] **地形/植被视距**  
  Quality Mobile：`terrainBasemapDistance`、`terrainTreeDistance` 下调。

### P2 — 锦上添花

- [ ] **同种怪 GPU Instancing / 合批**  
  相同 FBX+材质启用 instancing；远距关 shadow caster。

- [ ] **Rooster LOD 网格**  
  中远距换低面数或 impostor。

- [ ] **命中 VFX 对象池**  
  `CharacterActionPlayer` 运行时粒子改池化；限制同屏粒子数。

- [ ] **精英攻击范围圈移动端默认关**  
  `PunchRangeDisplay` 仅调试/PC 开。

- [ ] **分帧更新 AI（CPU，保留数量）**  
  近距 tier 内仍可进一步按 instanceId 错开 heavy 逻辑（中/远档已每 3 帧）。

### 验证

- [ ] 真机 iPhone Profiler：对比 Scripts / RenderLoop / Draw Calls  
- [ ] 区分 Editor（PC_RPAsset）与真机（Mobile_RPAsset）再下结论

---

## 内容 / 玩法

1. **写游戏小说**
   - 写一个关于这个游戏的小说
   - 小说需要有宏大的故事背景（为灵根/DNA 系统、世界观做铺垫）
   - 2026-09-02 新增

2. **攻击特效**
   - 人物攻击的特效，围绕人物的特效
   - 参考来源：B站上有个「蓝胖子」UP主，有特效相关的素材可以下载

3. **地图风格调成卡通**
   - 当前游戏地图色彩太写实，想要卡通化风格
