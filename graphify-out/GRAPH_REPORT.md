# Graph Report - GestionnairePressePapiers  (2026-11-16)

## Corpus Check
- 8 files · ~2,565 words
- Verdict: corpus is large enough that graph structure adds value.
- Unclassified: 2 file(s) not represented in the graph (top: (none) 1, .ico 1)

## Summary
- 85 nodes · 143 edges · 14 communities (4 shown, 10 thin omitted)
- Extraction: 94% EXTRACTED · 6% INFERRED · 0% AMBIGUOUS · INFERRED: 8 edges (avg confidence: 0.85)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `7b1a069c`
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
- History.cs

## God Nodes (most connected - your core abstractions)
1. `MainForm` - 41 edges
2. `Item` - 22 edges
3. `History` - 11 edges
4. `PressePapiers` - 4 edges
5. `Kind` - 4 edges
6. `Gestionnaire de presse-papiers` - 4 edges
7. `Program` - 2 edges
8. `RoundRect()` - 2 edges
9. `Draw()` - 2 edges
10. `Text` - 1 edges

## Surprising Connections (you probably didn't know these)
- `MainForm` --references--> `Item`  [EXTRACTED]
  src/PressePapiers/MainForm.cs → src/PressePapiers/History.cs
- `MainForm` --references--> `History`  [EXTRACTED]
  src/PressePapiers/MainForm.cs → src/PressePapiers/History.cs

## Import Cycles
- None detected.

## Communities (14 total, 10 thin omitted)

### Community 0 - "Item"
Cohesion: 0.22
Nodes (6): Item, Key, Kind, Pinned, Text, Time

### Community 4 - "Gestionnaire de presse-papiers"
Cohesion: 0.40
Nodes (4): Gestionnaire de presse-papiers, Installation, Tests, Utilisation

### Community 10 - "History"
Cohesion: 0.43
Nodes (3): History, Items, Max

### Community 12 - "History.cs"
Cohesion: 0.40
Nodes (3): Kind, Image, Text

## Knowledge Gaps
- **14 isolated node(s):** `Text`, `Image`, `Kind`, `Text`, `Key` (+9 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 35 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **10 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `MainForm` connect `MainForm` to `Item`, `IntPtr`, `MainForm.cs`, `.View`, `.DeleteImages`, `.ImagePath`, `History`, `.WndProc`?**
  _High betweenness centrality (0.500) - this node is a cross-community bridge._
- **Are the 3 inferred relationships involving `MainForm` (e.g. with `.ClearUnpinned()` and `.Load()`) actually correct?**
  _`MainForm` has 3 INFERRED edges - model-reasoned connections that need verification._
- **What connects `Text`, `Image`, `Kind` to the rest of the system?**
  _14 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Why does `Item` connect `Item` to `MainForm`, `.View`, `.DeleteImages`, `.ImagePath`, `History`, `History.cs`?**
  _High betweenness centrality (0.214) - this node is a cross-community bridge._
- **Are the 2 inferred relationships involving `Item` (e.g. with `.CaptureClipboard()` and `.CopySelected()`) actually correct?**
  _`Item` has 2 INFERRED edges - model-reasoned connections that need verification._
- **Why does `History` connect `History` to `Item`, `MainForm`, `.View`, `.DeleteImages`, `History.cs`?**
  _High betweenness centrality (0.072) - this node is a cross-community bridge._