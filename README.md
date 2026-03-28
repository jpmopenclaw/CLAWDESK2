# CLAWDESK 🦞

OpenClaw 桌面控制中心 - Windows 桌面客戶端

## 功能

- 💬 **對話功能** - 直接與 OpenClaw Agent 對話
- ⚙️ **模型設定** - 切換 AI Provider、輸入 API Key、切換模型
- 📁 **檔案瀏覽** - 瀏覽 workspace 檔案並預覽內容
- ⚡ **指令執行** - 快速執行常用指令
- 🔄 **CI/CD** - GitHub Actions 自動建置測試

## 技術棧

- .NET 8 + WPF
- MVVM 架構
- HTTP Client 對接 OpenClaw Gateway

## 快速開始

### 前置需求

- .NET 8 SDK
- Windows 10/11
- OpenClaw Gateway 執行中

### 建置

```bash
dotnet restore
dotnet build --configuration Release
```

### 執行

```bash
dotnet run
```

或直接執行編譯後的 `.exe` 檔案

## 設定

首次啟動後，在「設定」頁面填入：
- **Provider**: OpenAI / Anthropic / Google / MiniMax 等
- **API Key**: 您的 API Key
- **Default Model**: 如 `gpt-4o`
- **Gateway URL**: OpenClaw Gateway 位址（預設 `http://localhost:8080`）

## 專案結構

```
CLAWDESK/
├── Models/           # 資料模型
├── Services/         # 服務層（API、檔案、設定）
├── ViewModels/       # ViewModel 層
├── Views/           # XAML 視窗
└── SPEC.md          # 詳細規格書
```

## License

MIT
