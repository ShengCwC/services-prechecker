using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Shell;

namespace UndefinedSS.ServicesPrechecker
{
    internal sealed class MainWindow : Window
    {
        private static readonly Color BackgroundColor = Color.FromRgb(7, 8, 8);
        private static readonly Color PanelColor = Color.FromRgb(14, 15, 15);
        private static readonly Color PanelRaisedColor = Color.FromRgb(19, 20, 19);
        private static readonly Color ChromeColor = Color.FromRgb(16, 18, 19);
        private static readonly Color BorderColor = Color.FromRgb(48, 45, 41);
        private static readonly Color PrimaryTextColor = Color.FromRgb(239, 237, 232);
        private static readonly Color SecondaryTextColor = Color.FromRgb(162, 157, 148);
        private static readonly Color AccentColor = Color.FromRgb(194, 184, 166);
        private static readonly Color AccentDarkColor = Color.FromRgb(30, 29, 27);
        private static readonly Color RestartColor = Color.FromRgb(231, 158, 76);

        private readonly bool autoEnable;
        private readonly Border windowFrame;
        private readonly UniformGrid servicesGrid;
        private readonly TextBlock summaryText;
        private readonly TextBlock metadataText;
        private readonly TextBlock activityText;
        private readonly TextBlock restartNoticeTitle;
        private readonly TextBlock restartNoticeBody;
        private readonly Border restartNotice;
        private readonly Button enableButton;
        private readonly Button refreshButton;
        private readonly Grid modalLayer;
        private readonly TextBlock modalTitle;
        private readonly TextBlock modalBody;
        private readonly Button modalConfirmButton;
        private bool isBusy;
        private bool restartPending;

        public MainWindow(bool autoEnable)
        {
            this.autoEnable = autoEnable;
            restartPending = RestartRequirement.IsPendingForCurrentBoot();

            Title = "Undefined SS · Services Prechecker";
            Width = 1140;
            Height = 760;
            MinWidth = 980;
            MinHeight = 700;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.CanResize;
            Background = new SolidColorBrush(BackgroundColor);
            Foreground = new SolidColorBrush(PrimaryTextColor);
            FontFamily = new FontFamily("Microsoft YaHei UI");
            FontSize = 13;
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;

            WindowChrome chrome = new WindowChrome
            {
                CaptionHeight = 44,
                ResizeBorderThickness = new Thickness(6),
                GlassFrameThickness = new Thickness(0),
                CornerRadius = new CornerRadius(11),
                UseAeroCaptionButtons = false
            };
            WindowChrome.SetWindowChrome(this, chrome);

            ImageSource icon = LoadEmbeddedImage("UndefinedSS.ServicesPrechecker.Assets.app-logo.png");
            if (icon != null)
            {
                Icon = icon;
            }

            SourceInitialized += HandleSourceInitialized;
            StateChanged += HandleWindowStateChanged;
            PreviewKeyDown += HandlePreviewKeyDown;

            Grid root = new Grid();
            Content = root;

            windowFrame = new Border
            {
                Background = new SolidColorBrush(BackgroundColor),
                BorderBrush = new SolidColorBrush(Color.FromRgb(63, 59, 53)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(11),
                ClipToBounds = true
            };
            root.Children.Add(windowFrame);

            Grid layout = new Grid();
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(44) });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(218) });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(44) });
            windowFrame.Child = layout;

            FrameworkElement titleBar = BuildTitleBar();
            Grid.SetRow(titleBar, 0);
            layout.Children.Add(titleBar);

            FrameworkElement hero = BuildHero();
            Grid.SetRow(hero, 1);
            layout.Children.Add(hero);

            Grid content = new Grid();
            content.Margin = new Thickness(22, 12, 22, 12);
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8) });
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) });
            content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(content, 2);
            layout.Children.Add(content);

            Grid toolbar = new Grid();
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            content.Children.Add(toolbar);

            StackPanel summaryPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            summaryText = new TextBlock
            {
                Text = "正在检查系统服务…",
                FontSize = 19,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(PrimaryTextColor)
            };
            metadataText = new TextBlock
            {
                Text = "读取本机服务控制管理器",
                Margin = new Thickness(0, 5, 0, 0),
                FontSize = 11,
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
            refreshButton.Margin = new Thickness(0, 0, 9, 0);
            refreshButton.Click += HandleRefreshClick;
            enableButton = CreateButton("一键启用全部系统服务", true);
            enableButton.Click += HandleEnableClick;
            actions.Children.Add(refreshButton);
            actions.Children.Add(enableButton);
            Grid.SetColumn(actions, 1);
            toolbar.Children.Add(actions);

            restartNoticeTitle = new TextBlock
            {
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(RestartColor)
            };
            restartNoticeBody = new TextBlock
            {
                FontSize = 11,
                Margin = new Thickness(0, 3, 0, 0),
                Foreground = new SolidColorBrush(Color.FromRgb(190, 183, 172)),
                TextWrapping = TextWrapping.Wrap
            };
            restartNotice = BuildRestartNotice();
            Grid.SetRow(restartNotice, 2);
            content.Children.Add(restartNotice);

            servicesGrid = new UniformGrid
            {
                Columns = 4,
                Rows = 2
            };
            Grid.SetRow(servicesGrid, 4);
            content.Children.Add(servicesGrid);

            Border footer = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(10, 11, 11)),
                BorderBrush = new SolidColorBrush(BorderColor),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(22, 0, 22, 0)
            };
            Grid footerLayout = new Grid();
            footerLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footerLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            activityText = new TextBlock
            {
                Text = "所有检查均在本机完成，不会上传任何数据。",
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(SecondaryTextColor),
                FontSize = 10
            };
            TextBlock versionText = new TextBlock
            {
                Text = "v1.2.0  ·  FORENSICS READINESS",
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(AccentColor),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 9,
                FontWeight = FontWeights.SemiBold
            };
            footerLayout.Children.Add(activityText);
            Grid.SetColumn(versionText, 1);
            footerLayout.Children.Add(versionText);
            footer.Child = footerLayout;
            Grid.SetRow(footer, 3);
            layout.Children.Add(footer);

            modalLayer = new Grid
            {
                Background = new SolidColorBrush(Color.FromArgb(205, 0, 0, 0)),
                Visibility = Visibility.Collapsed
            };
            WindowChrome.SetIsHitTestVisibleInChrome(modalLayer, true);
            root.Children.Add(modalLayer);

            Border modal = new Border
            {
                Width = 500,
                Background = new SolidColorBrush(Color.FromRgb(20, 21, 20)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(104, 82, 57)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(28, 24, 28, 24),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            StackPanel modalContent = new StackPanel();
            TextBlock modalEyebrow = new TextBlock
            {
                Text = "RESTART REQUIRED",
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 9,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(RestartColor)
            };
            modalTitle = new TextBlock
            {
                Text = "需要重启电脑",
                Margin = new Thickness(0, 9, 0, 0),
                FontSize = 24,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(PrimaryTextColor)
            };
            modalBody = new TextBlock
            {
                Margin = new Thickness(0, 13, 0, 0),
                FontSize = 12,
                LineHeight = 21,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromRgb(196, 190, 180))
            };
            TextBlock modalStrong = new TextBlock
            {
                Text = "请重启系统后，再开始下一次查端。",
                Margin = new Thickness(0, 13, 0, 0),
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(RestartColor)
            };
            modalConfirmButton = CreateButton("我知道了", true);
            modalConfirmButton.Margin = new Thickness(0, 22, 0, 0);
            modalConfirmButton.HorizontalAlignment = HorizontalAlignment.Right;
            modalConfirmButton.Click +=
                delegate
                {
                    modalLayer.Visibility = Visibility.Collapsed;
                    enableButton.Focus();
                };
            modalContent.Children.Add(modalEyebrow);
            modalContent.Children.Add(modalTitle);
            modalContent.Children.Add(modalBody);
            modalContent.Children.Add(modalStrong);
            modalContent.Children.Add(modalConfirmButton);
            modal.Child = modalContent;
            modalLayer.Children.Add(modal);

            UpdateRestartNotice();
            Loaded += HandleLoaded;
        }

        private FrameworkElement BuildTitleBar()
        {
            Grid titleBar = new Grid
            {
                Background = new SolidColorBrush(ChromeColor)
            };
            titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel trafficControls = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(16, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            trafficControls.Children.Add(
                CreateWindowControl(
                    Color.FromRgb(255, 95, 87),
                    "关闭",
                    delegate { Close(); }));
            trafficControls.Children.Add(
                CreateWindowControl(
                    Color.FromRgb(254, 188, 46),
                    "最小化",
                    delegate { WindowState = WindowState.Minimized; }));
            trafficControls.Children.Add(
                CreateWindowControl(
                    Color.FromRgb(40, 200, 64),
                    "最大化或还原",
                    ToggleMaximize));
            titleBar.Children.Add(trafficControls);

            StackPanel center = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            ImageSource icon = LoadEmbeddedImage("UndefinedSS.ServicesPrechecker.Assets.app-logo.png");
            if (icon != null)
            {
                center.Children.Add(
                    new Image
                    {
                        Source = icon,
                        Width = 17,
                        Height = 17,
                        Margin = new Thickness(0, 0, 8, 0)
                    });
            }
            center.Children.Add(
                new TextBlock
                {
                    Text = "Undefined SS · Services Prechecker",
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromRgb(208, 205, 199)),
                    VerticalAlignment = VerticalAlignment.Center
                });
            Grid.SetColumn(center, 1);
            titleBar.Children.Add(center);

            Border edition = new Border
            {
                BorderBrush = new SolidColorBrush(BorderColor),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(0, 0, 14, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            edition.Child = new TextBlock
            {
                Text = "WINDOWS  ·  1.2",
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 8,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(AccentColor)
            };
            Grid.SetColumn(edition, 2);
            titleBar.Children.Add(edition);

            return titleBar;
        }

        private FrameworkElement BuildHero()
        {
            Border hero = new Border
            {
                BorderBrush = new SolidColorBrush(BorderColor),
                BorderThickness = new Thickness(0, 0, 0, 1),
                ClipToBounds = true
            };

            ImageSource texture = LoadEmbeddedImage(
                "UndefinedSS.ServicesPrechecker.Assets.hero-texture.png");
            if (texture != null)
            {
                hero.Background = new ImageBrush(texture)
                {
                    Stretch = Stretch.UniformToFill,
                    AlignmentX = AlignmentX.Center,
                    AlignmentY = AlignmentY.Center,
                    Opacity = 0.86
                };
            }
            else
            {
                hero.Background = new SolidColorBrush(Color.FromRgb(8, 9, 9));
            }

            Grid heroLayers = new Grid();
            hero.Child = heroLayers;
            heroLayers.Children.Add(
                new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(62, 0, 0, 0))
                });

            Grid heroLayout = new Grid
            {
                Margin = new Thickness(32, 20, 32, 18)
            };
            heroLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(136) });
            heroLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
            heroLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            heroLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(244) });
            heroLayers.Children.Add(heroLayout);

            StackPanel identity = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left
            };

            ImageSource appLogo = LoadEmbeddedImage(
                "UndefinedSS.ServicesPrechecker.Assets.app-logo.png");
            if (appLogo != null)
            {
                identity.Children.Add(
                    new Image
                    {
                        Source = appLogo,
                        Width = 78,
                        Height = 78,
                        HorizontalAlignment = HorizontalAlignment.Left
                    });
            }

            identity.Children.Add(
                new TextBlock
                {
                    Text = "UNDEFINED SS",
                    Margin = new Thickness(0, 6, 0, 0),
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 9,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(AccentColor)
                });
            identity.Children.Add(
                new TextBlock
                {
                    Text = "SERVICES PRECHECKER",
                    Margin = new Thickness(0, 2, 0, 0),
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 7,
                    Foreground = new SolidColorBrush(SecondaryTextColor)
                });
            heroLayout.Children.Add(identity);

            Border identityDivider = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromArgb(96, 89, 82, 72)),
                BorderThickness = new Thickness(1, 0, 0, 0),
                Margin = new Thickness(13, 0, 13, 0)
            };
            Grid.SetColumn(identityDivider, 1);
            heroLayout.Children.Add(identityDivider);

            Grid copy = new Grid
            {
                Margin = new Thickness(8, 1, 28, 1)
            };
            copy.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            copy.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            copy.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            TextBlock eyebrow = new TextBlock
            {
                Text = "SYSTEM READINESS  /  01",
                Foreground = new SolidColorBrush(AccentColor),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 9,
                FontWeight = FontWeights.SemiBold
            };
            copy.Children.Add(eyebrow);

            StackPanel heroCopy = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center
            };
            heroCopy.Children.Add(
                new TextBlock
                {
                    Text = "系统服务预检",
                    FontSize = 31,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(PrimaryTextColor)
                });
            TextBlock statement = new TextBlock
            {
                Margin = new Thickness(0, 7, 0, 0),
                FontSize = 12,
                LineHeight = 20,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromRgb(190, 185, 176))
            };
            statement.Inlines.Add(new Run("电脑需启用所有所需系统服务并"));
            statement.Inlines.Add(
                new Run("重启系统")
                {
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(RestartColor)
                });
            statement.Inlines.Add(new Run("才可为后续查端做准备"));
            heroCopy.Children.Add(statement);
            Grid.SetRow(heroCopy, 1);
            copy.Children.Add(heroCopy);

            Border restartLine = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromArgb(105, 102, 82, 59)),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(0, 8, 0, 0)
            };
            StackPanel restartContent = new StackPanel
            {
                Orientation = Orientation.Horizontal
            };
            restartContent.Children.Add(
                new Ellipse
                {
                    Width = 6,
                    Height = 6,
                    Fill = new SolidColorBrush(RestartColor),
                    Margin = new Thickness(0, 0, 8, 0),
                    VerticalAlignment = VerticalAlignment.Center
                });
            restartContent.Children.Add(
                new TextBlock
                {
                    Text = "启用后必须重启  ·  CURRENT SESSION INVALID",
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 9,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(RestartColor)
                });
            restartLine.Child = restartContent;
            Grid.SetRow(restartLine, 2);
            copy.Children.Add(restartLine);
            Grid.SetColumn(copy, 2);
            heroLayout.Children.Add(copy);

            Border evidence = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromArgb(96, 89, 82, 72)),
                BorderThickness = new Thickness(1, 0, 0, 0),
                Padding = new Thickness(24, 2, 0, 1)
            };
            StackPanel evidenceContent = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center
            };
            evidenceContent.Children.Add(
                new TextBlock
                {
                    Text = "READINESS GATE",
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 8,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(RestartColor)
                });
            evidenceContent.Children.Add(
                new TextBlock
                {
                    Text = "01   SERVICE STATE",
                    Margin = new Thickness(0, 14, 0, 0),
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 9,
                    Foreground = new SolidColorBrush(AccentColor)
                });
            evidenceContent.Children.Add(
                new TextBlock
                {
                    Text = "02   BOOT VALIDITY",
                    Margin = new Thickness(0, 8, 0, 0),
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 9,
                    Foreground = new SolidColorBrush(AccentColor)
                });
            evidenceContent.Children.Add(
                new TextBlock
                {
                    Text = "03   FORENSIC SOURCES",
                    Margin = new Thickness(0, 8, 0, 0),
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 9,
                    Foreground = new SolidColorBrush(AccentColor)
                });
            evidenceContent.Children.Add(
                new TextBlock
                {
                    Text = "RESTART TO ESTABLISH A VALID BASELINE",
                    Margin = new Thickness(0, 15, 0, 0),
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 7,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(SecondaryTextColor)
                });
            evidence.Child = evidenceContent;
            Grid.SetColumn(evidence, 3);
            heroLayout.Children.Add(evidence);

            return hero;
        }

        private Border BuildRestartNotice()
        {
            Border notice = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(22, 18, 14)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(79, 59, 39)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(14, 9, 14, 9)
            };
            Grid body = new Grid();
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Border accent = new Border
            {
                Background = new SolidColorBrush(RestartColor),
                CornerRadius = new CornerRadius(2)
            };
            body.Children.Add(accent);

            StackPanel copy = new StackPanel();
            copy.Children.Add(restartNoticeTitle);
            copy.Children.Add(restartNoticeBody);
            Grid.SetColumn(copy, 2);
            body.Children.Add(copy);
            notice.Child = body;
            return notice;
        }

        private Button CreateWindowControl(Color color, string tooltip, Action action)
        {
            Button button = new Button
            {
                Width = 14,
                Height = 14,
                Margin = new Thickness(0, 0, 8, 0),
                Background = new SolidColorBrush(color),
                BorderBrush = new SolidColorBrush(Color.FromArgb(95, 0, 0, 0)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                FocusVisualStyle = null,
                ToolTip = tooltip,
                Template = BuildCircleButtonTemplate()
            };
            WindowChrome.SetIsHitTestVisibleInChrome(button, true);
            button.Click += delegate { action(); };
            AutomationProperties.SetName(button, tooltip);
            button.MouseEnter +=
                delegate
                {
                    button.Opacity = 0.78;
                };
            button.MouseLeave +=
                delegate
                {
                    button.Opacity = 1.0;
                };
            return button;
        }

        private async void HandleLoaded(object sender, RoutedEventArgs e)
        {
            await RefreshSnapshots();
            if (autoEnable)
            {
                await EnableAllServices();
            }
            else if (restartPending)
            {
                ShowRestartModal(-1);
            }
        }

        private async void HandleRefreshClick(object sender, RoutedEventArgs e)
        {
            restartPending = RestartRequirement.IsPendingForCurrentBoot();
            UpdateRestartNotice();
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
            string completedMessage = restartPending
                ? "服务设置已改变：请重启系统；当前启动周期的查端仍按异常处理。"
                : "检查完成。所有检查均在本机完成，不会上传任何数据。";
            SetBusy(false, completedMessage);
        }

        private async Task EnableAllServices()
        {
            if (isBusy || !ServiceManager.IsAdministrator())
            {
                return;
            }

            SetBusy(true, "正在配置取证所需服务，请稍候…");

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

            if (results.Any(delegate(EnableResult item) { return item.Success; }))
            {
                RestartRequirement.MarkPendingForCurrentBoot();
                restartPending = true;
            }

            IList<ServiceSnapshot> snapshots = await Task.Run(
                delegate
                {
                    return ServiceManager.GetSnapshots();
                });
            RenderSnapshots(snapshots);
            UpdateRestartNotice();

            int failed = results.Count(delegate(EnableResult item) { return !item.Success; });
            string message;
            if (failed == 0)
            {
                message = "全部系统服务已配置；必须重启电脑，后续查端才可生效。";
            }
            else
            {
                string failedNames = string.Join(
                    "、",
                    results.Where(delegate(EnableResult item) { return !item.Success; })
                           .Select(delegate(EnableResult item) { return item.Definition.DisplayName; }));
                message =
                    "部分服务已配置，但 " + failedNames +
                    " 未能启用。处理失败项目后仍必须重启电脑。";
            }

            SetBusy(false, message);
            ShowRestartModal(failed);
        }

        private void RenderSnapshots(IList<ServiceSnapshot> snapshots)
        {
            if (restartPending)
            {
                foreach (ServiceSnapshot snapshot in snapshots)
                {
                    if (snapshot.VisualState == ServiceVisualState.Running)
                    {
                        snapshot.VisualState = ServiceVisualState.RebootRequired;
                        snapshot.StatusText = "等待重启";
                        snapshot.Detail = "服务已运行，但仅在重启后的查端中生效";
                    }
                }
            }

            servicesGrid.Children.Clear();
            foreach (ServiceSnapshot snapshot in snapshots)
            {
                servicesGrid.Children.Add(BuildServiceCard(snapshot));
            }
            servicesGrid.Children.Add(BuildInformationCard());

            int healthy = snapshots.Count(
                delegate(ServiceSnapshot snapshot)
                {
                    return snapshot.VisualState == ServiceVisualState.Running;
                });
            int attention = snapshots.Count(
                delegate(ServiceSnapshot snapshot)
                {
                    return snapshot.VisualState != ServiceVisualState.Running;
                });

            if (restartPending)
            {
                summaryText.Text = "服务已配置，等待系统重启";
                summaryText.Foreground = new SolidColorBrush(RestartColor);
            }
            else if (healthy == snapshots.Count)
            {
                summaryText.Text = "7 / 7 项服务运行正常";
                summaryText.Foreground = new SolidColorBrush(Color.FromRgb(119, 205, 151));
            }
            else
            {
                summaryText.Text = healthy + " / " + snapshots.Count + " 项服务运行正常";
                summaryText.Foreground = new SolidColorBrush(PrimaryTextColor);
            }

            string privilege = ServiceManager.IsAdministrator() ? "已获得管理员权限" : "按需请求管理员权限";
            if (restartPending)
            {
                metadataText.Text =
                    "当前启动周期仍无效  ·  必须重启后再进行后续查端  ·  " + privilege;
            }
            else
            {
                metadataText.Text =
                    "最近检测 " + DateTime.Now.ToString("HH:mm:ss") +
                    "  ·  " + privilege +
                    (attention > 0 ? "  ·  " + attention + " 项需要处理" : "  ·  已具备服务运行条件");
            }
        }

        private FrameworkElement BuildServiceCard(ServiceSnapshot snapshot)
        {
            Border card = new Border
            {
                Background = new SolidColorBrush(PanelColor),
                BorderBrush = new SolidColorBrush(BorderColor),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Margin = new Thickness(5),
                Padding = new Thickness(14, 12, 14, 11)
            };

            Grid cardGrid = new Grid();
            cardGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            cardGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(5) });
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
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 0, 9, 0)
            };
            headingGrid.Children.Add(name);

            TextBlock serviceName = new TextBlock
            {
                Text = snapshot.Definition.ServiceName,
                Foreground = new SolidColorBrush(AccentColor),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 9,
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
                FontSize = 10,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 6)
            };
            Grid.SetRow(description, 2);
            cardGrid.Children.Add(description);

            TextBlock detail = new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(snapshot.Detail)
                    ? "启动方式：" + snapshot.StartTypeText
                    : snapshot.Detail,
                Foreground = new SolidColorBrush(Color.FromRgb(123, 120, 114)),
                FontSize = 9,
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
                Padding = new Thickness(7, 4, 7, 4),
                HorizontalAlignment = HorizontalAlignment.Left
            };

            StackPanel content = new StackPanel { Orientation = Orientation.Horizontal };
            Ellipse dot = new Ellipse
            {
                Width = 6,
                Height = 6,
                Fill = new SolidColorBrush(stateColor),
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            TextBlock label = new TextBlock
            {
                Text = snapshot.StatusText,
                Foreground = new SolidColorBrush(stateColor),
                FontSize = 9,
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
                CornerRadius = new CornerRadius(5),
                Margin = new Thickness(5),
                Padding = new Thickness(14, 12, 14, 11)
            };

            Grid layout = new Grid();
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            StackPanel heading = new StackPanel { Orientation = Orientation.Horizontal };
            ImageSource logo = LoadEmbeddedImage("UndefinedSS.ServicesPrechecker.Assets.app-logo.png");
            if (logo != null)
            {
                heading.Children.Add(
                    new Image
                    {
                        Source = logo,
                        Width = 27,
                        Height = 27,
                        Margin = new Thickness(0, 0, 9, 0)
                    });
            }
            StackPanel headingText = new StackPanel();
            headingText.Children.Add(
                new TextBlock
                {
                    Text = "重启后生效",
                    Foreground = new SolidColorBrush(PrimaryTextColor),
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold
                });
            headingText.Children.Add(
                new TextBlock
                {
                    Text = "RESTART GATE",
                    Foreground = new SolidColorBrush(RestartColor),
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 8,
                    FontWeight = FontWeights.SemiBold
                });
            heading.Children.Add(headingText);
            layout.Children.Add(heading);

            TextBlock body = new TextBlock
            {
                Text = "现在启用服务不会让本次查端变为有效。必须重启系统，后续查端才可生效。",
                Foreground = new SolidColorBrush(SecondaryTextColor),
                FontSize = 10,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(body, 1);
            layout.Children.Add(body);

            TextBlock note = new TextBlock
            {
                Text = "当前启动周期仍按异常处理",
                Foreground = new SolidColorBrush(RestartColor),
                FontSize = 9,
                FontWeight = FontWeights.SemiBold
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
                Height = 36,
                MinWidth = primary ? 184 : 100,
                Padding = new Thickness(15, 0, 15, 0),
                FontFamily = new FontFamily("Microsoft YaHei UI"),
                FontSize = 11,
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
                    if (button.IsEnabled)
                    {
                        button.Background = new SolidColorBrush(
                            primary ? Color.FromRgb(218, 209, 192) : Color.FromRgb(27, 28, 27));
                    }
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
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));

            FrameworkElementFactory presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            presenter.SetValue(ContentPresenter.RecognizesAccessKeyProperty, true);
            border.AppendChild(presenter);

            ControlTemplate template = new ControlTemplate(typeof(Button));
            template.VisualTree = border;
            return template;
        }

        private static ControlTemplate BuildCircleButtonTemplate()
        {
            FrameworkElementFactory ellipse = new FrameworkElementFactory(typeof(Ellipse));
            ellipse.SetValue(Shape.FillProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            ellipse.SetValue(Shape.StrokeProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
            ellipse.SetValue(Shape.StrokeThicknessProperty, 1.0);
            ControlTemplate template = new ControlTemplate(typeof(Button));
            template.VisualTree = ellipse;
            return template;
        }

        private void UpdateRestartNotice()
        {
            if (restartPending)
            {
                restartNotice.Background = new SolidColorBrush(Color.FromRgb(33, 20, 15));
                restartNotice.BorderBrush = new SolidColorBrush(Color.FromRgb(116, 62, 41));
                restartNoticeTitle.Text = "已配置服务，必须重启系统";
                restartNoticeBody.Text =
                    "当前启动周期内的查端仍按异常处理。请重启 Windows；只有重启后的后续查端才有效。";
            }
            else
            {
                restartNotice.Background = new SolidColorBrush(Color.FromRgb(22, 18, 14));
                restartNotice.BorderBrush = new SolidColorBrush(Color.FromRgb(79, 59, 39));
                restartNoticeTitle.Text = "启用任何所需服务后，都必须重启系统";
                restartNoticeBody.Text =
                    "如果现在才启用服务，本次查端仍按异常处理；只有重启后的后续查端才有效。";
            }
        }

        private void ShowRestartModal(int failedCount)
        {
            if (failedCount < 0)
            {
                modalTitle.Text = "仍需重启电脑";
                modalBody.Text =
                    "检测到服务设置是在本次 Windows 启动期间完成的。当前启动周期的查端仍会按照“异常”处理，不能用于后续查端判断。";
            }
            else if (failedCount == 0)
            {
                modalTitle.Text = "服务已配置，需要重启电脑";
                modalBody.Text =
                    "全部所需系统服务已完成配置。但这些服务不会让当前启动周期的查端变为有效，本次查端结果仍会按照“异常”处理。";
            }
            else
            {
                modalTitle.Text = "部分服务已配置";
                modalBody.Text =
                    "已完成可用的服务设置，但仍有服务未能启用。解决失败项目后也必须重启系统；当前启动周期的查端仍会按照“异常”处理。";
            }

            modalLayer.Visibility = Visibility.Visible;
            modalConfirmButton.Focus();
        }

        private void SetBusy(bool busy, string activity)
        {
            isBusy = busy;
            refreshButton.IsEnabled = !busy;
            enableButton.IsEnabled = !busy;
            enableButton.Content = busy ? "正在处理…" : "一键启用全部系统服务";
            activityText.Text = activity;
            Cursor = busy ? Cursors.Wait : Cursors.Arrow;
        }

        private void ToggleMaximize()
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private void HandleWindowStateChanged(object sender, EventArgs e)
        {
            windowFrame.CornerRadius = WindowState == WindowState.Maximized
                ? new CornerRadius(0)
                : new CornerRadius(11);
        }

        private void HandlePreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && modalLayer.Visibility == Visibility.Visible)
            {
                modalLayer.Visibility = Visibility.Collapsed;
                e.Handled = true;
            }
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
                    return Color.FromRgb(225, 158, 78);
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
            int darkMode = 1;
            DwmSetWindowAttribute(handle, 20, ref darkMode, sizeof(int));
            int roundedCorners = 2;
            DwmSetWindowAttribute(handle, 33, ref roundedCorners, sizeof(int));
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(
            IntPtr windowHandle,
            int attribute,
            ref int attributeValue,
            int attributeSize);
    }
}
