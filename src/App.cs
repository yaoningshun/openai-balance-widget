using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace Saldo {
    public sealed class SaldoWindow : Window {
        readonly Grid view;
        readonly bool demo;
        readonly Preferences preferences;
        readonly BalanceClient client = new BalanceClient();
        readonly CancellationTokenSource stop = new CancellationTokenSource();
        readonly DispatcherTimer timer = new DispatcherTimer();
        Forms.NotifyIcon tray;
        Balance balance;
        string session, state = "Connect your OpenAI account to get started.";
        bool expanded, busy, stale, dragging, closing;
        int failures, connectionVersion;
        Point press;
        public SaldoWindow(bool isDemo) {
            demo = isDemo;
            preferences = demo ? new Preferences() : Storage.Load();
            session = demo ? "" : Storage.ReadToken();
            Title = "Saldo · OpenAI balance";
            Width = preferences.Mode == "orb" ? 136 : 224;
            Height = preferences.Mode == "orb" ? 132 : 84;
            Left = preferences.Left; Top = preferences.Top;
            WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true; Background = Brushes.Transparent; Topmost = true;
            ShowInTaskbar = false; FontFamily = new FontFamily("Segoe UI");
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Saldo.View.xaml"))
                view = (Grid)XamlReader.Load(stream);
            Content = view;
            SetTheme(preferences.Theme);
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
            BindButton("SettingsButton", delegate { ShowSettings(); });
            BindButton("CloseDetailsButton", delegate { if (expanded) Toggle(); });
            BindButton("BillingButton", delegate { OpenBilling(); });
            BindButton("RefreshButton", async delegate { await Refresh(); });
            foreach (string name in new [] { "Pill", "Orb" }) {
                Border surface = Element<Border>(name);
                surface.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e) { press = e.GetPosition(this); dragging = false; e.Handled = true; };
                surface.MouseMove += delegate(object sender, MouseEventArgs e) {
                    if (e.LeftButton != MouseButtonState.Pressed || dragging) return;
                    Point p = e.GetPosition(this);
                    if (Math.Abs(p.X - press.X) + Math.Abs(p.Y - press.Y) < 6) return;
                    dragging = true;
                    try { DragMove(); } catch (InvalidOperationException) {}
                    RememberPosition();
                };
                surface.MouseLeftButtonUp += delegate { if (!dragging) Toggle(); };
                surface.ContextMenu = MakeMenu();
            }
            KeyDown += async delegate(object sender, KeyEventArgs e) {
                if (e.Key == Key.Escape && expanded) Toggle();
                if (e.Key == Key.F5) await Refresh();
                if (e.Key == Key.Space) Toggle();
            };
            timer.Tick += async delegate { await Refresh(); };
            Loaded += async delegate {
                ApplyMode(); Clamp();
                if (demo) { balance = Balance.Demo(); state = "Updated just now · refreshes every 2 min"; Render(); }
                else { SetupTray(); await Refresh(); timer.Start(); }
            };
            Closed += delegate {
                closing = true; timer.Stop(); stop.Cancel();
                if (tray != null) { tray.Visible = false; tray.Dispose(); }
                client.Dispose(); stop.Dispose();
            };
        }
        T Element<T>(string name) where T : FrameworkElement { return (T)view.FindName(name); }
        void BindButton(string name, Action action) { Element<Button>(name).Click += delegate { action(); }; }
        void Save() { if (!demo) Storage.Save(preferences); }
        void SetTheme(string theme) {
            bool dark = theme == "dark";
            string[] names = { "Surface", "Ink", "Muted", "Soft", "Line", "Accent" };
            string[] colors = dark ? new [] { "#17201F", "#F0F5F1", "#93A59C", "#222D29", "#344139", "#8CD9AE" }
                : new [] { "#FDFEFB", "#20362A", "#7F8E82", "#F0F4ED", "#E0E7DD", "#368C62" };
            for (int i = 0; i < names.Length; i++) Resources[names[i]] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colors[i]));
            preferences.Theme = theme;
        }
        void ApplyMode() {
            bool orb = preferences.Mode == "orb";
            Element<Border>("Pill").Visibility = orb ? Visibility.Collapsed : Visibility.Visible;
            Element<Border>("Orb").Visibility = orb ? Visibility.Visible : Visibility.Collapsed;
            Element<Border>("Card").Margin = new Thickness(16, orb ? 128 : 76, 16, 20);
            Resize();
        }
        void Resize() {
            double right = Left + Width;
            Width = expanded ? 336 : (preferences.Mode == "orb" ? 136 : 224);
            Height = expanded ? (preferences.Mode == "orb" ? 472 : 420) : (preferences.Mode == "orb" ? 132 : 84);
            Left = right - Width; Clamp();
        }
        void Clamp() {
            Left = Math.Max(SystemParameters.VirtualScreenLeft, Math.Min(Left, SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - Width));
            Top = Math.Max(SystemParameters.VirtualScreenTop, Math.Min(Top, SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - Height));
        }
        void RememberPosition() {
            Clamp();
            preferences.Left = Left + Width - (preferences.Mode == "orb" ? 136 : 224);
            preferences.Top = Top; Save();
        }
        public void Toggle() {
            expanded = !expanded;
            var card = Element<Border>("Card");
            card.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
            Resize();
            if (expanded) card.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(140)));
        }
        ContextMenu MakeMenu() {
            var menu = new ContextMenu();
            AddMenu(menu, "Details", delegate { Toggle(); });
            AddMenu(menu, "Refresh", async delegate { await Refresh(); });
            AddMenu(menu, "Settings", delegate { ShowSettings(); });
            menu.Items.Add(new Separator());
            AddMenu(menu, "Light / dark", delegate { SetTheme(preferences.Theme == "dark" ? "light" : "dark"); Render(); Save(); });
            AddMenu(menu, "Pill / orb", delegate { preferences.Mode = preferences.Mode == "orb" ? "pill" : "orb"; ApplyMode(); RememberPosition(); });
            menu.Items.Add(new Separator());
            AddMenu(menu, "Quit Saldo", delegate { Close(); });
            return menu;
        }
        static void AddMenu(ContextMenu menu, string label, Action action) { var item = new MenuItem { Header = label }; item.Click += delegate { action(); }; menu.Items.Add(item); }
        void SetupTray() {
            tray = new Forms.NotifyIcon { Text = "Saldo · OpenAI balance", Visible = true, Icon = System.Drawing.Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location) };
            var menu = new Forms.ContextMenuStrip();
            menu.Items.Add("Show balance", null, delegate { Dispatcher.Invoke(new Action(delegate { if (!expanded) Toggle(); Activate(); })); });
            menu.Items.Add("Settings", null, delegate { Dispatcher.Invoke(new Action(ShowSettings)); });
            menu.Items.Add("Quit", null, delegate { Dispatcher.Invoke(new Action(Close)); });
            tray.ContextMenuStrip = menu;
            tray.DoubleClick += delegate { Dispatcher.Invoke(new Action(delegate { Toggle(); Activate(); })); };
        }
        async Task Refresh() {
            if (demo || busy || closing) return;
            if (String.IsNullOrEmpty(session)) { state = "Connect in Settings. Your session stays on this PC."; Render(); SetInterval(); return; }
            int version = connectionVersion;
            busy = true; Element<Button>("RefreshButton").IsEnabled = false;
            Element<Button>("RefreshButton").Content = "Refreshing…";
            try {
                var result = await client.Fetch(session, preferences.Organization, stop.Token);
                if (version != connectionVersion || closing) return;
                balance = result;
                failures = 0; stale = false;
                state = "Updated " + balance.Updated.ToString("HH:mm:ss") + " · auto refresh on";
            } catch (Exception e) {
                if (closing || version != connectionVersion) return;
                failures++; stale = true;
                state = e is BalanceException ? e.Message : "Could not update. Your previous balance is unchanged.";
            } finally {
                busy = false;
                if (!closing) { Element<Button>("RefreshButton").IsEnabled = true; Element<Button>("RefreshButton").Content = "Refresh ↻"; Render(); SetInterval(); }
            }
        }
        void SetInterval() { timer.Interval = TimeSpan.FromSeconds(Math.Min(900, Math.Max(60, preferences.RefreshSeconds) * Math.Pow(2, Math.Min(failures, 3)))); }
        void Render() {
            string text = balance == null ? "Connect" : Balance.Money(balance.Available);
            Element<TextBlock>("PillAmount").Text = text;
            Element<TextBlock>("PillAmount").FontSize = text.Length > 10 ? 14 : 18;
            Element<TextBlock>("OrbAmount").Text = balance == null ? "—" : text;
            Element<TextBlock>("OrbAmount").FontSize = text.Length > 8 ? 15 : 22;
            Element<TextBlock>("Amount").Text = balance == null ? "—" : text;
            Element<TextBlock>("Amount").FontSize = text.Length > 10 ? 30 : 42;
            Element<TextBlock>("Paid").Text = balance != null && balance.Paid.HasValue ? Balance.Money(balance.Paid.Value) : "—";
            Element<TextBlock>("Other").Text = balance != null && balance.Other.HasValue ? Balance.Money(balance.Other.Value) : "—";
            string message = state;
            if (stale && balance != null) message = "Last known balance · " + balance.Updated.ToString("HH:mm") + "\n" + state;
            Element<TextBlock>("Status").Text = message;
            if (expanded) {
                UpdateLayout();
                var card = Element<Border>("Card");
                Height = Math.Max(preferences.Mode == "orb" ? 472 : 420, card.ActualHeight + card.Margin.Top + 24);
                Clamp();
            }
            Element<Border>("Pill").ToolTip = message + "\nClick for details · drag to move · right-click for options";
            Element<Border>("Orb").ToolTip = message;
            Color color = stale ? Color.FromRgb(196, 131, 62) : (balance != null && balance.Available < 5 ? Color.FromRgb(191, 106, 83) : ((SolidColorBrush)Resources["Accent"]).Color);
            Element<System.Windows.Shapes.Ellipse>("StatusDot").Fill = new SolidColorBrush(color);
            if (tray != null) tray.Text = ("Saldo · " + text + (stale ? " (last known)" : ""));
        }
        static void OpenBilling() { Process.Start(new ProcessStartInfo("https://platform.openai.com/settings/organization/billing/overview") { UseShellExecute = true }); }
        void ShowSettings() {
            if (demo) return;
            var dialog = new Window { Title = "Connect · Saldo", Width = 410, Height = 600, ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterScreen, Owner = this, Background = (Brush)Resources["Surface"], Foreground = (Brush)Resources["Ink"], FontFamily = FontFamily, Topmost = true };
            var stack = new StackPanel { Margin = new Thickness(26) }; dialog.Content = stack;
            stack.Children.Add(new TextBlock { Text = "A quiet home for your credits.", FontSize = 22, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
            stack.Children.Add(new TextBlock { Text = "OpenAI balance requires a browser session (sess-). This is an unofficial billing integration; a regular API key cannot read your credit balance.", Margin = new Thickness(0, 16, 0, 12), TextWrapping = TextWrapping.Wrap, FontSize = 12 });
            var billing = new Button { Content = "1. Open OpenAI billing", Padding = new Thickness(10), Margin = new Thickness(0, 0, 0, 12) }; billing.Click += delegate { OpenBilling(); }; stack.Children.Add(billing);
            stack.Children.Add(new TextBlock { Text = "2. Paste browser session", FontSize = 12 });
            var password = new PasswordBox { Height = 32, Padding = new Thickness(6), Margin = new Thickness(0, 6, 0, 5) }; stack.Children.Add(password);
            stack.Children.Add(new TextBlock { Text = "In billing: F12 → Network → credit_grants → request Authorization. Copy the sess- value. Stored with Windows account encryption; sent only to OpenAI.", TextWrapping = TextWrapping.Wrap, FontSize = 11, Foreground = (Brush)Resources["Muted"] });
            stack.Children.Add(new TextBlock { Text = "Organization ID (optional)", Margin = new Thickness(0, 16, 0, 5), FontSize = 12 });
            var org = new TextBox { Text = preferences.Organization, Padding = new Thickness(6), Height = 30 }; stack.Children.Add(org);
            var status = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 11, Margin = new Thickness(0, 10, 0, 8) }; stack.Children.Add(status);
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            var connect = new Button { Content = "Connect", Padding = new Thickness(24, 9, 24, 9) };
            connect.Click += async delegate {
                string candidate = password.Password.Trim();
                if (candidate.Length == 0) candidate = session;
                connect.IsEnabled = false; status.Text = "Checking connection…";
                try {
                    var result = await client.Fetch(candidate, org.Text.Trim(), stop.Token);
                    Storage.SaveToken(candidate); connectionVersion++; session = candidate; preferences.Organization = org.Text.Trim(); Save();
                    balance = result; stale = false; failures = 0; state = "Connected · updated " + result.Updated.ToString("HH:mm:ss"); Render(); password.Clear(); dialog.Close();
                } catch (Exception e) { status.Text = e is BalanceException ? e.Message : "Connection could not be saved. Please try again."; }
                finally { connect.IsEnabled = true; }
            };
            row.Children.Add(connect);
            var forget = new Button { Content = "Disconnect", Margin = new Thickness(12, 0, 0, 0), Padding = new Thickness(12, 9, 12, 9) };
            forget.Click += delegate { Storage.ForgetToken(); connectionVersion++; session = ""; balance = null; stale = false; state = "Disconnected. Your saved session was removed."; Render(); dialog.Close(); };
            row.Children.Add(forget); stack.Children.Add(row); dialog.ShowDialog();
        }
        public void DemoTheme(string theme) { SetTheme(theme); Render(); }
        public void DemoMode(string mode) { preferences.Mode = mode; ApplyMode(); }
        public void Capture(string path) {
            UpdateLayout();
            var bitmap = new RenderTargetBitmap((int)(ActualWidth * 2), (int)(ActualHeight * 2), 192, 192, PixelFormats.Pbgra32);
            bitmap.Render(view);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var output = File.Create(path)) encoder.Save(output);
        }
    }
    public static class Program {
        static Mutex singleton;
        [STAThread] public static void Main(string[] args) {
            bool demo = Array.IndexOf(args, "--demo") >= 0;
            if (!demo) {
                bool created;
                singleton = new Mutex(true, "Local\\Saldo-" + Environment.UserName, out created);
                if (!created) return;
            }
            System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;
            var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
            var window = new SaldoWindow(demo);
            if (demo) window.Loaded += delegate {
                if (Array.IndexOf(args, "--pill") >= 0) window.DemoMode("pill");
                if (Array.IndexOf(args, "--dark") >= 0) window.DemoTheme("dark");
                if (Array.IndexOf(args, "--expanded") >= 0) window.Toggle();
                int capture = Array.IndexOf(args, "--capture");
                if (capture >= 0 && capture + 1 < args.Length) {
                    var renderTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
                    renderTimer.Tick += delegate { renderTimer.Stop(); window.Capture(args[capture + 1]); window.Close(); };
                    renderTimer.Start();
                }
            };
            app.Run(window);
            if (singleton != null) { singleton.ReleaseMutex(); singleton.Dispose(); }
        }
    }
}
