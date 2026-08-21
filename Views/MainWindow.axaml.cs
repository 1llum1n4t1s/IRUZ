using Avalonia.Controls;
using IRUZ.Services;

namespace IRUZ.Views
{
    /// <summary>
    /// メインウィンドウ。
    /// </summary>
    public partial class MainWindow : Window
    {
        private bool _shouldStartMinimized;

        /// <summary>
        /// MainWindow のコンストラクタ。
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();

            // 透過効果 OFF / リモートデスクトップではアクリルを不透明背景へ差し替える
            AcrylicFallbackHelper.Attach(this);

            Loaded += (_, _) =>
            {
                if (_shouldStartMinimized)
                {
                    _shouldStartMinimized = false; // 2回目以降の Loaded で再度隠されないようリセット
                    WindowState = WindowState.Minimized;
                    ShowInTaskbar = false;
                    Hide();
                }
            };
        }

        /// <summary>
        /// 起動時に最小化状態で開始することを指定するメソッド。
        /// </summary>
        internal void SetStartMinimized()
        {
            _shouldStartMinimized = true;
        }
    }
}
