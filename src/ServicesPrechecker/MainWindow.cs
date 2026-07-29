using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace UndefinedSS.ServicesPrechecker
{
    internal sealed class MainWindow : Window
    {
        private static readonly Color BackgroundColor = Color.FromRgb(7, 8, 8);
        private static readonly Color PanelColor = Color.FromRgb(14, 15, 15);
        private static readonly Color PanelRaisedColor = Color.FromRgb(19, 19, 18);
        private static readonly Color BorderColor = Color.FromRgb(48, 45, 41);
        private static readonly Color PrimaryTextColor = Color.FromRgb(239, 237, 232);
        private static readonly Color SecondaryTextColor = Color.FromRgb(162, 157, 148);
        private static readonly Color AccentColor = Color.FromRgb(190, 180, 162);
        private static readonly Color AccentDarkColor = Color.FromRgb(30, 29, 27);

        private readonly bool autoEnable;
        private readonly UniformGrid servicesGrid;
        private readonly TextBlock summaryText;
        private readonly TextBlock metadataText;
        private readonly TextBlock activityText;
        private readonly Button enableButton;
        private readonly Button refreshButton;
        private bool isBusy;

        public MainWindow(bool autoEnable)
        {
            this.autoEnable = autoEnable;

            Title = "Undefined SS · Services Prechecker";
            Width = 1120;
            Height = 790;
            MinWidth = 940;
            MinHeight = 700;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = new SolidColorBrush(BackgroundColor);
            Foreground = new SolidColorBrush(PrimaryTextColor);
            FontFamily = new FontFamily("Microsoft YaHei UI");
            FontSize = 13;
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;

            ImageSource icon = LoadEmbeddedImage("UndefinedSS.ServicesPrechecker.Assets.app.ico");
            if (icon != null)
            {
                Icon = icon;
            }

            SourceInitialized += HandleSourceInitialized;

            Grid root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(238) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(48) });
            Content = root;

            FrameworkElement hero = BuildHero();
            Grid.SetRow(hero, 0);
            root.Children.Add(hero);

            Grid content = new Grid();
            content.Margin = new Thickness(24, 18, 24, 16);
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(14) });
            content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(content, 1);
            root.Children.Add(content);

            Grid toolbar = new Grid();
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetRow(toolbar, 0);
            content.Children.Add(toolbar);

            StackPanel summaryPanel = new StackPanel();
            summaryText = new TextBlock
            {
                Text = "正在检查系统服务…",
                FontSize = 20,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(PrimaryTextColor)
            };
            metadataText = new TextBlock
            {
                Text = "读取本机服务控制管理器",
                Margin = new Thickness(0, 6, 0, 0),
                FontSize = 12,
                Foreground = new SolidColorBrush(SecondaryTextColor)
            };
            summaryPanel.Children.Add(summaryText);
            summaryPanel.Children.Add(metadataText);
            toolbar.Children.Add(summaryPanel);

            StackPanel actions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            refreshButton = CreateButton("重新检测", false);
            refreshButton.Margin = new Thickness(0, 0, 10, 0);
            refreshButton.Click += HandleRefreshClick;
            enableButton = CreateButton("一键启用全部服务", true);
            enableButton.Click += HandleEnableClick;
            actions.Children.Add(refreshButton);
            actions.Children.Add(enableButton);
            Grid.SetColumn(actions, 1);
            toolbar.Children.Add(actions);

            servicesGrid = new UniformGrid
            {
                Columns = 4,
                Rows = 2
            };
            Grid.SetRow(servicesGrid, 2);
            content.Children.Add(servicesGrid);

            Border footer = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(10, 11, 11)),
                BorderBrush = new SolidColorBrush(BorderColor),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(24, 0, 24, 0)
            };
            Grid footerLayout = new Grid();
            footerLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footerLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            activityText = new TextBlock
            {
                Text = "所有检查均在本机完成，不会上传任何数据。",
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(SecondaryTextColor),
                FontSize = 11
            };
            TextBlock versionText = new TextBlock
            {
                Text = "v1.0.0  ·  FORENSICS READINESS",
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(AccentColor),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 10,
                FontWeight = FontWeights.SemiBold
            };
            footerLayout.Children.Add(activityText);
            Grid.SetColumn(versionText, 1);
            footerLayout.Children.Add(versionText);
            footer.Child = footerLayout;
            Grid.SetRow(footer, 2);
            root.Children.Add(footer);

            Loaded += HandleLoaded;
        }

        private FrameworkElement BuildHero()
        {
            Border hero = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(8, 8, 8)),
                BorderBrush = new SolidColorBrush(BorderColor),
                BorderThickness = new Thickness(0, 0, 0, 1),
                ClipToBounds = true
            };

            Grid heroGrid = new Grid();
            ImageSource banner = LoadEmbeddedImage("UndefinedSS.ServicesPrechecker.Assets.banner.png");
            if (banner != null)
            {
                heroGrid.Background = new ImageBrush(banner)
                {
                    Stretch = Stretch.UniformToFill,
                    AlignmentX = AlignmentX.Center,
                    AlignmentY = AlignmentY.Center,
                    Opacity = 0.82
                };
            }

            Border shade = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(76, 0, 0, 0))
            };
            heroGrid.Children.Add(shade);

            Grid overlay = new Grid { Margin = new Thickness(26, 20, 26, 20) };
            overlay.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            overlay.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            overlay.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            TextBlock eyebrow = new TextBlock
            {
                Text = "UNDEFINED SS COMMUNITY  /  SERVICES PRECHECKER",
                Foreground = new SolidColorBrush(AccentColor),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 10,
                FontWeight = FontWeights.SemiBold
            };
            overlay.Children.Add(eyebrow);

            Border forensicBadge = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(185, 8, 8, 8)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(150, 124, 116, 104)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12, 7, 12, 7),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top
            };
            TextBlock badgeText = new TextBlock
            {
                Text = "SCREENSHARE · FORENSICS · EVIDENCE",
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 9,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(AccentColor)
            };
            forensicBadge.Child = badgeText;
            overlay.Children.Add(forensicBadge);

            StackPanel titlePanel = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Bottom
            };
            TextBlock title = new TextBlock
            {
                Text = "系统服务预检",
                FontSize = 26,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(PrimaryTextColor)
            };
            TextBlock subtitle = new TextBlock
            {
                Text = "在远程查端开始前，确认取证所需的 Windows 数据源可用",
                Margin = new Thickness(0, 7, 0, 0),
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(197, 192, 184))
            };
            titlePanel.Children.Add(title);
            titlePanel.Children.Add(subtitle);
            Grid.SetRow(titlePanel, 2);
            overlay.Children.Add(titlePanel);

            heroGrid.Children.Add(overlay);
            hero.Child = heroGrid;
            return hero;
        }

        private async void HandleLoaded(object sender, RoutedEventArgs e)
        {
            await RefreshSnapshots();
            if (autoEnable)
            {
                await EnableAllServices();
            }
        }

        private async void HandleRefreshClick(object sender, RoutedEventArgs e)
        {
            await RefreshSnapshots();
        }

        private async void HandleEnableClick(object sender, RoutedEventArgs e)
        {
            if (!ServiceManager.IsAdministrator())
            {
                try
                {
                    activityText.Text = "正在请求管理员权限…";
                    ServiceManager.RelaunchElevated();
                    Close();
                }
                catch (Win32Exception exception)
                {
                    if (exception.NativeErrorCode == 1223)
                    {
                        activityText.Text = "已取消管理员授权，未对系统服务进行更改。";
                    }
                    else
                    {
                        activityText.Text = "无法请求管理员权限：" + exception.Message;
                    }
                }

                return;
            }

            await EnableAllServices();
        }

        private async Task RefreshSnapshots()
        {
            if (isBusy)
            {
                return;
            }

            SetBusy(true, "正在读取 7 项系统服务…");
            IList<ServiceSnapshot> snapshots;
            try
            {
                snapshots = await Task.Run(
                    delegate
                    {
                        return ServiceManager.GetSnapshots();
                    });
            }
            catch (Exception exception)
            {
                SetBusy(false, "检测失败：" + exception.Message);
                return;
            }

            RenderSnapshots(snapshots);
            SetBusy(false, "检查完成。所有检查均在本机完成，不会上传任何数据。");
        }

        private async Task EnableAllServices()
        {
            if (isBusy)
            {
                return;
            }

            if (!ServiceManager.IsAdministrator())
            {
                return;
            }

            SetBusy(true, "正在启用并启动取证所需服务，请稍候…");

            IList<EnableResult> results;
            try
            {
                results = await Task.Run(
                    delegate
                    {
                        return ServiceManager.EnableAll();
                    });
            }
            catch (Exception exception)
            {
                SetBusy(false, "启用失败：" + exception.Message);
                return;
            }

            IList<ServiceSnapshot> snapshots = await Task.Run(
                delegate
                {
                    return ServiceManager.GetSnapshots();
                });

            foreach (EnableResult result in results.Where(delegate(EnableResult item) { return item.RequiresRestart; }))
            {
                ServiceSnapshot snapshot = snapshots.FirstOrDefault(
                    delegate(ServiceSnapshot item)
                    {
                        return string.Equals(
                            item.Definition.ServiceName,
                            result.Definition.ServiceName,
                            StringComparison.OrdinalIgnoreCase);
                    });
                if (snapshot != null && !snapshot.IsHealthy)
                {
                    snapshot.VisualState = ServiceVisualState.RebootRequired;
                    snapshot.StatusText = "需要重启";
                    snapshot.Detail = result.Message;
                }
            }

            RenderSnapshots(snapshots);

            int failed = results.Count(delegate(EnableResult item) { return !item.Success; });
            int restart = results.Count(delegate(EnableResult item) { return item.RequiresRestart; });
            string message;
            if (failed == 0 && restart == 0)
            {
                message = "全部服务已启用并开始运行，可以继续查端。";
            }
            else if (failed == 0)
            {
                message = "服务配置已完成；其中 " + restart + " 项需要重启 Windows 后生效。";
            }
            else
            {
                string failedNames = string.Join(
                    "、",
                    results.Where(delegate(EnableResult item) { return !item.Success; })
                           .Select(delegate(EnableResult item) { return item.Definition.DisplayName; }));
                message = "有 " + failed + " 项未能启用：" + failedNames + "。";
            }

            SetBusy(false, message);
        }

        private void RenderSnapshots(IList<ServiceSnapshot> snapshots)
        {
            servicesGrid.Children.Clear();
            foreach (ServiceSnapshot snapshot in snapshots)
            {
                servicesGrid.Children.Add(BuildServiceCard(snapshot));
            }

            servicesGrid.Children.Add(BuildInformationCard());

            int healthy = snapshots.Count(delegate(ServiceSnapshot snapshot) { return snapshot.IsHealthy; });
            if (healthy == snapshots.Count)
            {
                summaryText.Text = "7 / 7 项服务运行正常";
                summaryText.Foreground = new SolidColorBrush(Color.FromRgb(119, 205, 151));
            }
            else
            {
                summaryText.Text = healthy + " / " + snapshots.Count + " 项服务运行正常";
                summaryText.Foreground = new SolidColorBrush(PrimaryTextColor);
            }

            int attention = snapshots.Count(delegate(ServiceSnapshot snapshot) { return !snapshot.IsHealthy; });
            string privilege = ServiceManager.IsAdministrator() ? "已获得管理员权限" : "按需请求管理员权限";
            metadataText.Text =
                "最近检测 " + DateTime.Now.ToString("HH:mm:ss") +
                "  ·  " + privilege +
                (attention > 0 ? "  ·  " + attention + " 项需要处理" : "  ·  已具备采集条件");
        }

        private FrameworkElement BuildServiceCard(ServiceSnapshot snapshot)
        {
            Border card = new Border
            {
                Background = new SolidColorBrush(PanelColor),
                BorderBrush = new SolidColorBrush(BorderColor),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(6),
                Padding = new Thickness(16, 14, 16, 12)
            };

            Grid cardGrid = new Grid();
            cardGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            cardGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(6) });
            cardGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            cardGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            cardGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Grid headingGrid = new Grid();
            headingGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headingGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            TextBlock name = new TextBlock
            {
                Text = snapshot.Definition.DisplayName,
                Foreground = new SolidColorBrush(PrimaryTextColor),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 0, 10, 0)
            };
            headingGrid.Children.Add(name);

            TextBlock serviceName = new TextBlock
            {
                Text = snapshot.Definition.ServiceName,
                Foreground = new SolidColorBrush(AccentColor),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 10,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(serviceName, 1);
            headingGrid.Children.Add(serviceName);
            cardGrid.Children.Add(headingGrid);

            TextBlock description = new TextBlock
            {
                Text = snapshot.Definition.Description,
                Foreground = new SolidColorBrush(SecondaryTextColor),
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            };
            Grid.SetRow(description, 2);
            cardGrid.Children.Add(description);

            TextBlock detail = new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(snapshot.Detail)
                    ? "启动方式：" + snapshot.StartTypeText
                    : snapshot.Detail,
                Foreground = new SolidColorBrush(Color.FromRgb(123, 120, 114)),
                FontSize = 10,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Top
            };
            Grid.SetRow(detail, 3);
            cardGrid.Children.Add(detail);

            Border statePill = BuildStatePill(snapshot);
            Grid.SetRow(statePill, 4);
            cardGrid.Children.Add(statePill);

            card.Child = cardGrid;
            return card;
        }

        private Border BuildStatePill(ServiceSnapshot snapshot)
        {
            Color stateColor = GetStateColor(snapshot.VisualState);
            Border pill = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(25, stateColor.R, stateColor.G, stateColor.B)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(105, stateColor.R, stateColor.G, stateColor.B)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(8, 5, 8, 5),
                HorizontalAlignment = HorizontalAlignment.Left
            };

            StackPanel content = new StackPanel { Orientation = Orientation.Horizontal };
            Ellipse dot = new Ellipse
            {
                Width = 7,
                Height = 7,
                Fill = new SolidColorBrush(stateColor),
                Margin = new Thickness(0, 0, 7, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            TextBlock label = new TextBlock
            {
                Text = snapshot.StatusText,
                Foreground = new SolidColorBrush(stateColor),
                FontSize = 10,
                FontWeight = FontWeights.SemiBold
            };
            content.Children.Add(dot);
            content.Children.Add(label);
            pill.Child = content;
            return pill;
        }

        private FrameworkElement BuildInformationCard()
        {
            Border card = new Border
            {
                Background = new SolidColorBrush(PanelRaisedColor),
                BorderBrush = new SolidColorBrush(Color.FromRgb(72, 66, 58)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(6),
                Padding = new Thickness(16, 14, 16, 12)
            };

            Grid layout = new Grid();
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            StackPanel heading = new StackPanel { Orientation = Orientation.Horizontal };
            ImageSource logo = LoadEmbeddedImage("UndefinedSS.ServicesPrechecker.Assets.app.ico");
            if (logo != null)
            {
                Image logoImage = new Image
                {
                    Source = logo,
                    Width = 28,
                    Height = 28,
                    Stretch = Stretch.UniformToFill,
                    Margin = new Thickness(0, 0, 10, 0)
                };
                heading.Children.Add(logoImage);
            }

            StackPanel headingText = new StackPanel();
            headingText.Children.Add(
                new TextBlock
                {
                    Text = "查端前准备",
                    Foreground = new SolidColorBrush(PrimaryTextColor),
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold
                });
            headingText.Children.Add(
                new TextBlock
                {
                    Text = "FORENSICS READINESS",
                    Foreground = new SolidColorBrush(AccentColor),
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 8,
                    FontWeight = FontWeights.SemiBold
                });
            heading.Children.Add(headingText);
            layout.Children.Add(heading);

            TextBlock body = new TextBlock
            {
                Text = "启用这些服务只恢复 Windows 数据源，不会开始远程连接、采集或上传数据。",
                Foreground = new SolidColorBrush(SecondaryTextColor),
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(body, 1);
            layout.Children.Add(body);

            TextBlock note = new TextBlock
            {
                Text = "BAM 驱动可能需要重启后生效",
                Foreground = new SolidColorBrush(Color.FromRgb(188, 176, 156)),
                FontSize = 10
            };
            Grid.SetRow(note, 2);
            layout.Children.Add(note);

            card.Child = layout;
            return card;
        }

        private Button CreateButton(string text, bool primary)
        {
            Button button = new Button
            {
                Content = text,
                Height = 38,
                MinWidth = primary ? 174 : 104,
                Padding = new Thickness(16, 0, 16, 0),
                FontFamily = new FontFamily("Microsoft YaHei UI"),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand,
                FocusVisualStyle = null,
                Background = new SolidColorBrush(primary ? AccentColor : PanelColor),
                Foreground = new SolidColorBrush(primary ? AccentDarkColor : PrimaryTextColor),
                BorderBrush = new SolidColorBrush(primary ? AccentColor : BorderColor),
                BorderThickness = new Thickness(1),
                Template = BuildButtonTemplate()
            };

            button.MouseEnter +=
                delegate
                {
                    if (!button.IsEnabled)
                    {
                        return;
                    }

                    button.Background = new SolidColorBrush(
                        primary ? Color.FromRgb(213, 204, 187) : Color.FromRgb(26, 26, 25));
                };
            button.MouseLeave +=
                delegate
                {
                    button.Background = new SolidColorBrush(primary ? AccentColor : PanelColor);
                };

            return button;
        }

        private static ControlTemplate BuildButtonTemplate()
        {
            FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
            border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(3));

            FrameworkElementFactory presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            presenter.SetValue(ContentPresenter.RecognizesAccessKeyProperty, true);
            border.AppendChild(presenter);

            ControlTemplate template = new ControlTemplate(typeof(Button));
            template.VisualTree = border;
            return template;
        }

        private void SetBusy(bool busy, string activity)
        {
            isBusy = busy;
            refreshButton.IsEnabled = !busy;
            enableButton.IsEnabled = !busy;
            enableButton.Content = busy ? "正在处理…" : "一键启用全部服务";
            activityText.Text = activity;
            Cursor = busy ? Cursors.Wait : Cursors.Arrow;
        }

        private static Color GetStateColor(ServiceVisualState state)
        {
            switch (state)
            {
                case ServiceVisualState.Running:
                    return Color.FromRgb(113, 202, 146);
                case ServiceVisualState.Disabled:
                    return Color.FromRgb(225, 106, 102);
                case ServiceVisualState.RebootRequired:
                    return Color.FromRgb(175, 143, 221);
                case ServiceVisualState.Missing:
                case ServiceVisualState.Error:
                    return Color.FromRgb(139, 139, 139);
                default:
                    return Color.FromRgb(226, 166, 91);
            }
        }

        private static ImageSource LoadEmbeddedImage(string resourceName)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            using (System.IO.Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    return null;
                }

                BitmapImage image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = stream;
                image.EndInit();
                image.Freeze();
                return image;
            }
        }

        private void HandleSourceInitialized(object sender, EventArgs e)
        {
            IntPtr handle = new WindowInteropHelper(this).Handle;
            int enabled = 1;
            DwmSetWindowAttribute(handle, 20, ref enabled, sizeof(int));
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(
            IntPtr windowHandle,
            int attribute,
            ref int attributeValue,
            int attributeSize);
    }
}
