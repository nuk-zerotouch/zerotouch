# ZeroTouch（導航/HMI 零接觸控制面板）

ZeroTouch 是一套以 **.NET / C#** 開發的跨平台導航/HMI（Human–Machine Interface）面板，採用 **Avalonia UI** 建構桌面應用程式介面，並整合 **毫米波雷達（mmWave）** 作為零接觸人機輸入（例如手勢、距離/存在感測），用於駕駛座互動、導航面板操作與情境模擬。

專案同時整合：
- **Mapsui**：地圖顯示與導航相關視覺化
- **LibVLCSharp**：影音播放/串流（音樂播放媒體）

> 目標：在不接觸螢幕的情境下，以 mmWave 感測輸入驅動 UI 互動，提升可用性與衛生性，並降低行車分心操作。

---

## 功能特色（Features）

- **零接觸控制（mmWave）**
  - 以 mmWave 感測結果映射成 UI 操作（例：切換頁面、縮放、確認/返回、滑動）

- **導航與地圖顯示**
  - 地圖圖層渲染、選擇路線等
  - 可用於導航面板或模擬器 UI

- **多媒體整合**
  - 透過 LibVLCSharp 播放本地音樂
  - 可作為 HMI 面板中的媒體模組

- **跨平台 UI**
  - 使用 Avalonia UI，具備跨平台功能（Windows / Linux / macOS）

---

## 系統需求（Requirements）

- .NET SDK：`8.0`
- 作業系統：Windows / Linux / macOS
- mmWave 裝置：
  - 裝置型號：K60168A Dongle
- VLC/播放相依性（LibVLC）：
  - Windows

---

## 專案結構（Project Structure）

```
.
├── ATTRIBUTIONS.md
├── CODE_OF_CONDUCT.md
├── CONTRIBUTING.md
├── LICENSE
├── README.md
├── ZeroTouch.UI
│   ├── App.axaml
│   ├── App.axaml.cs
│   ├── Assets
│   ├── Converters
│   ├── Navigation
│   ├── Program.cs
│   ├── Services
│   ├── ViewLocator.cs
│   ├── ViewModels
│   ├── Views
│   ├── ZeroTouch.UI.csproj
│   ├── app.manifest
│   └── tests
├── ZeroTouch.sln
├── docs
│   └── deployment
├── packages.lock.json
├── scripts
│   └── run-jetson.sh
└── training
    ├── converter
    └── src

```

---

## 快速開始（Getting Started）

### 1) 取得程式碼
```bash
git clone https://github.com/nuk-zerotouch/zerotouch.git
cd zerotouch
```

### 2) 還原與建置
```bash
dotnet restore
dotnet build -c Release
```

### 3) 執行（開發模式）
```bash
dotnet run --project ZeroTouch.UI -c Debug
```

---

## 文件（Documentation）

- 部署與執行：`docs/deployment/`

---

## 開發指引（Development）

### 建議工具
- Visual Studio 2022 / JetBrains Rider
- .NET SDK
- Avalonia

---

## 授權（License）

本專案採用 **MIT License**。詳見 [LICENSE](./LICENSE)。

---

## 致謝（Attributions）

第三方套件與授權聲明請參考 `ATTRIBUTIONS.md`。
