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

            // カスタムタイトルバー：ドラッグ・最小化・閉じる
            PART_TitleBar.PointerPressed += (_, e) =>
            {
                if (!e.Handled && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                    BeginMoveDrag(e);
            };
            PART_Minimize.Click += (_, _) => WindowState = WindowState.Minimized;
            PART_Close.Click += (_, _) => Close();

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