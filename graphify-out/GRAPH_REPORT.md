# Graph Report - GestionnairePressePapiers  (2026-11-16)

## Corpus Check
- 8 files · ~2,388 words
- Verdict: corpus is large enough that graph structure adds value.
- Unclassified: 2 file(s) not represented in the graph (top: (none) 1, .ico 1)

## Summary
- 83 nodes · 137 edges · 12 communities (4 shown, 8 thin omitted)
- Extraction: 95% EXTRACTED · 5% INFERRED · 0% AMBIGUOUS · INFERRED: 7 edges (avg confidence: 0.85)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `ec83f613`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- Item
- MainForm
- MainForm.cs
- Gestionnaire de presse-papiers
- SelfTest.csproj
- PressePapiers.csproj
- History
- make_icon.ps1

## God Nodes (most connected - your core abstractions)
1. `MainForm` - 37 edges
2. `Item` - 20 edges
3. `History` - 11 edges
4. `PressePapiers` - 4 edges
5. `Kind` - 4 edges
6. `Gestionnaire de presse-papiers` - 4 edges
7. `Program` - 2 edges
8. `RoundRect()` - 2 edges
9. `Draw()` - 2 edges
10. `Text` - 1 edges

## Surprising Connections (you probably didn't know these)
- `MainForm` --references--> `History`  [EXTRACTED]
  src/PressePapiers/MainForm.cs → src/PressePapiers/History.cs

## Import Cycles
- None detected.

## Communities (12 total, 8 thin omitted)

### Community 0 - "Item"
Cohesion: 0.16
Nodes (6): Item, Key, Kind, Pinned, Text, Time

### Community 3 - "MainForm.cs"
Cohesion: 0.13
Nodes (5): PressePapiers, Kind, Image, Text, Program

### Community 4 - "Gestionnaire de presse-papiers"
Cohesion: 0.40
Nodes (4): Gestionnaire de presse-papiers, Installation, Tests, Utilisation

### Community 10 - "History"
Cohesion: 0.43
Nodes (3): History, Items, Max

## Knowledge Gaps
- **13 isolated node(s):** `Text`, `Image`, `Kind`, `Text`, `Key` (+8 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 34 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **8 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `MainForm` connect `MainForm` to `Item`, `IntPtr`, `MainForm.cs`, `.GetForegroundWindow`, `.DeleteImages`, `.SetForegroundWindow`, `History`?**
  _High betweenness centrality (0.470) - this node is a cross-community bridge._
- **Are the 3 inferred relationships involving `MainForm` (e.g. with `.ClearUnpinned()` and `.Load()`) actually correct?**
  _`MainForm` has 3 INFERRED edges - model-reasoned connections that need verification._
- **What connects `Text`, `Image`, `Kind` to the rest of the system?**
  _13 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `MainForm.cs` be split into smaller, more focused modules?**
  _Cohesion score 0.13333333333333333 - nodes in this community are weakly interconnected._
- **Why does `Item` connect `Item` to `.DeleteImages`, `History`, `MainForm.cs`?**
  _High betweenness centrality (0.198) - this node is a cross-community bridge._
- **Why does `History` connect `History` to `.DeleteImages`, `Item`, `MainForm.cs`, `MainForm`?**
  _High betweenness centrality (0.102) - this node is a cross-community bridge._