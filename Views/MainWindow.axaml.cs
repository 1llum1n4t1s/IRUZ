using Avalonia.Controls;

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
            Loaded += (_, _) =>
            {
                if (_shouldStartMinimized)
                {
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