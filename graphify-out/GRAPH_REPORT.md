# Graph Report - GestionnairePressePapiers  (2026-11-16)

## Corpus Check
- 7 files · ~1,718 words
- Verdict: corpus is large enough that graph structure adds value.
- Unclassified: 1 file(s) not represented in the graph (top: (none) 1)

## Summary
- 74 nodes · 118 edges · 8 communities (3 shown, 5 thin omitted)
- Extraction: 94% EXTRACTED · 6% INFERRED · 0% AMBIGUOUS · INFERRED: 7 edges (avg confidence: 0.85)
- Token cost: 0 input · 0 output

## Community Hubs (Navigation)
- Item
- MainForm
- MainForm.cs
- Gestionnaire de presse-papiers
- History.cs
- SelfTest.csproj
- PressePapiers.csproj

## God Nodes (most connected - your core abstractions)
1. `MainForm` - 30 edges
2. `Item` - 17 edges
3. `History` - 11 edges
4. `PressePapiers` - 4 edges
5. `Kind` - 4 edges
6. `Gestionnaire de presse-papiers` - 4 edges
7. `Program` - 2 edges
8. `Text` - 1 edges
9. `Image` - 1 edges
10. `Kind` - 1 edges

## Surprising Connections (you probably didn't know these)
- `MainForm` --references--> `History`  [EXTRACTED]
  src/PressePapiers/MainForm.cs → src/PressePapiers/History.cs

## Import Cycles
- None detected.

## Communities (8 total, 5 thin omitted)

### Community 0 - "Item"
Cohesion: 0.16
Nodes (9): History, Items, Max, Item, Key, Kind, Pinned, Text (+1 more)

### Community 4 - "Gestionnaire de presse-papiers"
Cohesion: 0.40
Nodes (4): Gestionnaire de presse-papiers, Installation, Tests, Utilisation

### Community 5 - "History.cs"
Cohesion: 0.40
Nodes (3): Kind, Image, Text

## Knowledge Gaps
- **13 isolated node(s):** `Text`, `Image`, `Kind`, `Text`, `Key` (+8 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 33 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **5 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `MainForm` connect `MainForm` to `Item`, `IntPtr`, `MainForm.cs`?**
  _High betweenness centrality (0.481) - this node is a cross-community bridge._
- **Are the 3 inferred relationships involving `MainForm` (e.g. with `.ClearUnpinned()` and `.Load()`) actually correct?**
  _`MainForm` has 3 INFERRED edges - model-reasoned connections that need verification._
- **What connects `Text`, `Image`, `Kind` to the rest of the system?**
  _13 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Why does `Item` connect `Item` to `MainForm`, `History.cs`?**
  _High betweenness centrality (0.216) - this node is a cross-community bridge._
- **Why does `History` connect `Item` to `MainForm`, `History.cs`?**
  _High betweenness centrality (0.125) - this node is a cross-community bridge._