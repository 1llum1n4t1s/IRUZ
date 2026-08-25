# IRUZ リポジトリ作業規約

このファイルはリポジトリ全体に適用する。実装の構造と設計上の理由は [DESIGN.md](DESIGN.md) を正本とし、変更前に関連箇所を確認する。

## 技術構成と配置

- 対象は Windows x64。製品は .NET 10、Avalonia 12、CommunityToolkit.Mvvm、Velopack を使う Native AOT デスクトップアプリである。
- `Program.cs` は Velopack 初期化、単一インスタンス制御、自動更新、復帰通知を担当する。
- `App.axaml.cs` はウィンドウとトレイのライフタイム、明示終了、リソース解放を担当する。
- `ViewModels/` は画面とトレイで共有する状態・コマンド・タイマー制御を持ち、`Services/` は Windows API、レジストリ、トレイ同期などの境界を持つ。
- `Views/` と `Resources/` は表示とテーマに限定する。コードビハインドにはウィンドウ固有の表示制御だけを置く。
- `tests/IRUZ.Tests/` は xUnit v3 と Avalonia Headless による正常系・境界・競合・ライフタイムの回帰テストである。
- `web/` はランディングページ用 Cloudflare Worker であり、デスクトップアプリのビルドおよび Velopack 配信とは独立して扱う。

## 実装規約

- UI とトレイは同じ `MainWindowViewModel` を状態の正本として共有する。設定や状態を別系統で保持するときは、ViewModel の変更通知から同期する。
- `System.Timers.Timer` のコールバックから UI 状態を変更するときは `Dispatcher.UIThread` へ渡す。停止・再開と競合する処理は現在のタイマー参照を照合する。
- ジグル停止では `_jiggleGate` 配下で入力許可を先に落としてからタイマーを破棄し、停止完了後に `SendInput` が漏れない契約を維持する。
- 自動解除は絶対時刻で判定する。解除時はジグルを停止し、解除予定を破棄して、選択を `AutoStopOption.None` へ戻す。
- 最小化とタイトルバーの閉じる操作はトレイ格納として扱う。プロセス終了は明示的な「終了」操作または OS のシャットダウン要求から行う。
- 単一インスタンスのイベントを mutex より先に作る。UI の `Loaded` 前や更新再起動をまたぐ復帰要求は `WindowRestoreCoordinator` と `--restore-window` で保持する。
- Velopack のブートストラップは多重起動判定より先に実行する。更新失敗やタイムアウト時は現行バージョンで起動を継続する。
- スタートアップ登録は `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` の `IRUZ` 値を使う。Velopack 配下では更新で差し替わらないルートのスタブ exe を優先し、テストホストから登録パスを書き換えない。
- Windows API、タイマー、イベント購読、トレイアイコンは終了時に解放する。`Dispose` は複数回呼ばれても安全な形を維持する。
- アクリル背景は OS の透過無効時とリモートセッションで不透明背景へ切り替える。テーマ色は共有リソースへバインドしてライト／ダーク切替に追従させる。
- コード内コメントとテスト名は既存に合わせて日本語で記述する。警告とビルド時コードスタイル違反はエラーとして扱う。

## 依存関係とバージョン

- 製品バージョンの正本は `Directory.Build.props` の `<Version>` だけとする。通常の修正では変更せず、リリース時に `/vava` の手順で更新する。
- NuGet の更新時はルートとテストプロジェクト双方の `packages.lock.json` を同期し、locked restore が通る状態にする。
- Native AOT と trimming の互換性を維持し、動的コード生成や実行時だけ必要になる未注釈のリフレクションを追加するときは AOT publish まで検証する。

## 必須検証

デスクトップアプリの変更後はリポジトリルートで次を実行する。

```powershell
dotnet restore IRUZ.slnx --locked-mode
dotnet build IRUZ.slnx --configuration Release --no-restore
dotnet test IRUZ.slnx --configuration Release --no-build --verbosity normal
git diff --check
```

起動、Windows API、依存関係、publish 設定、更新処理へ影響する変更では Native AOT も検証する。

```powershell
dotnet publish IRUZ.csproj --configuration Release --runtime win-x64 --no-restore -p:OS=Windows_NT
```

- タイマーやライフタイムを変更した場合は正常系に加え、停止／再開、古いコールバック、並行実行、複数回 `Dispose` の回帰テストを追加または更新する。
- スタートアップ登録のテストは既存値を復元し、利用者のレジストリ状態を残さない。
- PR の `.NET ビルド` workflow は Windows 上でアイコン生成、restore、Release build、test を実行する。ローカルでも同じ範囲を完了してから渡す。

## 配布境界

- 署名付きリリースは `vava.config.json` から `scripts/release-local.ps1` を呼ぶ既存経路を使う。SimplySign Desktop の接続、コード署名証明書、既定の Cloudflare token が前提となる。
- リリーススクリプトは win-x64 の Native AOT publish、Velopack pack、署名検証、`iruz-updates` への R2 upload、固定 URL の cache purge、配信確認、旧成果物整理を一続きで行う。
- `web/**` の push はランディング Worker の deploy workflow を起動する。`/` と `/index.html` 以外は R2 配信へ透過的に委譲する契約を維持する。
- `iruz.kagayoi.com` と互換用の `iruz.nephilim.jp` の Worker route、Velopack の `velopack.IRUZ` 識別子、更新 URL は既存利用者との互換性に関わるため維持する。
