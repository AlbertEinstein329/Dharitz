# Graph Report - .  (2026-05-15)

## Corpus Check
- Corpus is ~15,885 words - fits in a single context window. You may not need a graph.

## Summary
- 234 nodes · 324 edges · 20 communities (11 shown, 9 thin omitted)
- Extraction: 97% EXTRACTED · 3% INFERRED · 0% AMBIGUOUS · INFERRED: 10 edges (avg confidence: 0.9)
- Token cost: 0 input · 0 output

## Community Hubs (Navigation)
- [[_COMMUNITY_Grid Spatial Logic|Grid Spatial Logic]]
- [[_COMMUNITY_Game Loop & Turns|Game Loop & Turns]]
- [[_COMMUNITY_Data Models & Config|Data Models & Config]]
- [[_COMMUNITY_UI Interaction Layer|UI Interaction Layer]]
- [[_COMMUNITY_Score & Cloud Services|Score & Cloud Services]]
- [[_COMMUNITY_HUD & Display|HUD & Display]]
- [[_COMMUNITY_Main Menu System|Main Menu System]]
- [[_COMMUNITY_Cell & Interfaces|Cell & Interfaces]]
- [[_COMMUNITY_Architecture Docs|Architecture Docs]]
- [[_COMMUNITY_Audio Manager|Audio Manager]]
- [[_COMMUNITY_Game Interfaces|Game Interfaces]]
- [[_COMMUNITY_Pattern Validator|Pattern Validator]]
- [[_COMMUNITY_Auth Manager|Auth Manager]]
- [[_COMMUNITY_Roadmap & Planning|Roadmap & Planning]]
- [[_COMMUNITY_Survival Algorithm|Survival Algorithm]]
- [[_COMMUNITY_Bag & Re-Draw|Bag & Re-Draw]]
- [[_COMMUNITY_Placement Rules|Placement Rules]]
- [[_COMMUNITY_Monetization & Skins|Monetization & Skins]]
- [[_COMMUNITY_Animation & Feedback|Animation & Feedback]]
- [[_COMMUNITY_Dice Roller|Dice Roller]]

## God Nodes (most connected - your core abstractions)
1. `GameManager` - 39 edges
2. `GridManager` - 37 edges
3. `UIManager` - 23 edges
4. `MainMenuManager` - 17 edges
5. `CellComponent` - 16 edges
6. `UIDieInteractor` - 15 edges
7. `int` - 12 edges
8. `PatternUIManager` - 12 edges
9. `PopUpManager` - 11 edges
10. `ScoreManager` - 10 edges

## Surprising Connections (you probably didn't know these)
- `Topological Survival Check` --semantically_similar_to--> `Survival Calculation Algorithm`  [INFERRED] [semantically similar]
  Sistema de Reglamento (Lógica de Supervivencia).txt → Resumen.txt
- `Score Calculator` --semantically_similar_to--> `Real-Time Scoring System`  [INFERRED] [semantically similar]
  Fases de progamacion.txt → Resumen.txt
- `Placement Rules` --semantically_similar_to--> `Adjacency and Color Rules`  [INFERRED] [semantically similar]
  Fases de progamacion.txt → Sistema de Reglamento (Lógica de Supervivencia).txt
- `AI Bot Heuristic System` --semantically_similar_to--> `ML-Agents Integration`  [INFERRED] [semantically similar]
  Fases de progamacion.txt → Informe de pendientes.txt
- `Multiplayer Architecture` --semantically_similar_to--> `Online Multiplayer Netcode`  [INFERRED] [semantically similar]
  Fases de progamacion.txt → Informe de pendientes.txt

## Hyperedges (group relationships)
- **Pattern System Pipeline** — resumen_pattern_validation, patternvalidator_patternvalidator, patterndata_patterndata, variantdata_variantdata, specialruleevaluator_specialruleevaluator [EXTRACTED 0.95]
- **Board Survival Logic** — resumen_survival_calculation, resumen_gap_penalty, gridmanager_gridmanager, survival_topological_survival, survival_adjacency_rules [INFERRED 0.85]
- **Interface Segregation System** — gameinterfaces_igridvalidator, gameinterfaces_iturnprovider, gameinterfaces_iplacementexecutor, cellcomponent_cellcomponent, gridmanager_gridmanager [EXTRACTED 0.95]

## Communities (20 total, 9 thin omitted)

### Community 1 - "Game Loop & Turns"
Cohesion: 0.11
Nodes (5): Button, Coroutine, GridManager, GroupData, GameManager

### Community 2 - "Data Models & Config"
Cohesion: 0.11
Nodes (17): bool, Dictionary, DieColor, int, List, ScriptableObject, GroupData, PlayerData (+9 more)

### Community 3 - "UI Interaction Layer"
Cohesion: 0.09
Nodes (12): Canvas, CanvasGroup, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler, RectTransform, PatternUIManager (+4 more)

### Community 4 - "Score & Cloud Services"
Cohesion: 0.12
Nodes (7): Camera, float, GameObject, MonoBehaviour, CloudSaveManager, PopUpManager, ScoreManager

### Community 5 - "HUD & Display"
Cohesion: 0.15
Nodes (5): DiceCounterUI, Image, UIManager, Sequence, TextMeshProUGUI

### Community 7 - "Cell & Interfaces"
Cohesion: 0.15
Nodes (6): Color, IGridValidator, IPlacementExecutor, ITurnProvider, CellComponent, SpriteRenderer

### Community 8 - "Architecture Docs"
Cohesion: 0.15
Nodes (13): Pattern UI Manager Design, Dynamic Results Panel, Session Config Design, Score Calculator, Pattern Variant 1 (Base), Pattern Variant 2 (Medium), Pattern Variant 3 (Hard), Diagonal Leap Mechanic (+5 more)

### Community 9 - "Audio Manager"
Cohesion: 0.25
Nodes (3): AudioClip, AudioSource, AudioManager

### Community 10 - "Game Interfaces"
Cohesion: 0.25
Nodes (3): IGridValidator, IPlacementExecutor, ITurnProvider

### Community 13 - "Roadmap & Planning"
Cohesion: 0.33
Nodes (6): AI Bot Heuristic System, Multiplayer Architecture, Campaign Mode, ML-Agents Integration, Online Multiplayer Netcode, Version Roadmap v0.2-1.0

### Community 14 - "Survival Algorithm"
Cohesion: 0.67
Nodes (3): Gap Penalty Dead Zone Detection, Survival Calculation Algorithm, Topological Survival Check

## Knowledge Gaps
- **38 isolated node(s):** `AudioSource`, `AudioClip`, `SpriteRenderer`, `Color`, `GridManager` (+33 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **9 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `GridManager` connect `Grid Spatial Logic` to `Data Models & Config`, `Score & Cloud Services`, `Cell & Interfaces`?**
  _High betweenness centrality (0.205) - this node is a cross-community bridge._
- **Why does `GameManager` connect `Game Loop & Turns` to `Data Models & Config`, `Score & Cloud Services`, `HUD & Display`, `Main Menu System`, `Cell & Interfaces`?**
  _High betweenness centrality (0.199) - this node is a cross-community bridge._
- **Why does `UIManager` connect `HUD & Display` to `Data Models & Config`, `UI Interaction Layer`, `Score & Cloud Services`?**
  _High betweenness centrality (0.109) - this node is a cross-community bridge._
- **What connects `AudioSource`, `AudioClip`, `SpriteRenderer` to the rest of the system?**
  _38 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Grid Spatial Logic` be split into smaller, more focused modules?**
  _Cohesion score 0.11 - nodes in this community are weakly interconnected._
- **Should `Game Loop & Turns` be split into smaller, more focused modules?**
  _Cohesion score 0.11 - nodes in this community are weakly interconnected._
- **Should `Data Models & Config` be split into smaller, more focused modules?**
  _Cohesion score 0.11 - nodes in this community are weakly interconnected._