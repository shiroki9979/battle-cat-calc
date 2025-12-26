# battle-cat-calc

Battle Cats calculator

## 專案目錄架構 (Project Structure)

本專案採用業界推薦的 `src` 佈局 (Src Layout)，這是目前 Python 最專業且通用的架構：

```text
battle-cat-calc/
├── src/                    # 原始碼目錄
│   └── battle_cat_calc/    # 主要套件目錄 (使用底線命名)
│       ├── __init__.py     # 將目錄標記為 Python 套件
│       └── main.py         # 程式入口點
├── tests/                  # 測試目錄
│   ├── __init__.py
│   └── test_example.py     # 單元測試文件
├── notebooks/              # Jupyter Notebooks (實驗、原型開發)
├── docs/                   # 專案文件 (Markdown, Sphinx 等)
├── assets/                 # 靜態資源 (圖片、數據檔、JSON 等)
├── .gitignore              # Git 忽略清單
├── pyproject.toml          # Poetry 專案配置與依賴管理
└── README.md               # 專案說明文件
```

### 目錄說明：
- **`src/`**: 所有的核心邏輯都放在這裡。使用 `src` 佈局可以避免一些導入路徑的混淆，並確保測試是在安裝後的環境下執行。
- **`tests/`**: 存放所有測試代碼。專業專案會將測試與原始碼分開。
- **`assets/`**: 存放計算器需要的貓咪數據、圖片等非程式碼資源。
- **`pyproject.toml`**: 這是現代 Python 的標準配置檔案，取代了舊有的 `setup.py`。

## 如何開始
1. 安裝依賴：`poetry install`
2. 執行程式：`poetry run python src/battle_cat_calc/main.py`