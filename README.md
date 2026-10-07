# GAUGE — Training Tracker

Minimalist, offline-first Android workout tracker built with **C# / .NET 8** and **Avalonia UI**. Designed for seamless workout logging, calisthenics tracking, and local Markdown/Obsidian export without cloud dependencies.

---

## Features
- **Offline-First Storage:** All logs, custom exercises, and sets are stored locally in human-readable JSON formats.
- **Smart Workout Interface:** Fast exercise search, one-tap category toggles (Upper / Core / Legs), and LRU (Last Recently Used) sorting.
- **Rest & Rep Management:** Dual slider controls and precision timers for tracking execution and rest durations.
- **Activity Streak Heatmap:** Autonomous monthly calendar highlighting training days independently of exported logs.
- **Obsidian & Markdown Export:** Clean, formatted `.md` generation ready to sync directly with your personal knowledge base.
- **Dark Performance Aesthetic:** Minimalist high-contrast UI tailored for focused gyand street workout sessions.

---

## Tech Stack
- **Framework:** Avalonia UI (Cross-platform XAML)
- **Runtime:** .NET 8 (Android)
- **Architecture:** MVVM Pattern via `CommunityToolkit.Mvvm`
- **Data:** Local File System Storage (JSON / Markdown)
