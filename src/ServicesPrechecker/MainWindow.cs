using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
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
using System.Windows.Markup;
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
        private static readonly string ApplicationVersion = GetApplicationVersion();

        private readonly bool autoEnable;
        private readonly string targetUserSid;
        private readonly Border windowFrame;
        private readonly UniformGrid servicesGrid;
        private readonly TextBlock summaryText;
        private readonly TextBlock metadataText;
        private readonly TextBlock activityText;
        private readonly Button hardwareIdButton;
        private readonly TextBlock hardwareIdText;
        private readonly TextBlock restartNoticeTitle;
        private readonly TextBlock restartNoticeBody;
        private readonly Border restartNotice;
        private readonly Button enableButton;
        private readonly Button refreshButton;
        private readonly Grid modalLayer;
        private readonly Border modalPanel;
        private readonly TextBlock modalEyebrow;
        private readonly TextBlock modalTitle;
        private readonly TextBlock modalBody;
        private readonly TextBlock modalStrong;
        private readonly Button modalSecondaryButton;
        private readonly Button modalConfirmButton;
        private bool isBusy;
        private bool restartPending;
        private string hardwareIdValue;
        private int hardwareIdFeedbackGeneration;
        private bool isHardwareIdCopying;
        private ModalPurpose modalPurpose;
        private IInputElement focusBeforeModal;
        private UpdateCheckResult activeUpdateResult;
        private UpdateCheckResult pendingUpdateResult;

        private enum ModalPurpose
        {
            None,
            Restart,
            Update
        }

        public MainWindow(bool autoEnable, string targetUserSid)
        {
            this.autoEnable = autoEnable;
            this.targetUserSid = targetUserSid;
            restartPending =
                RestartRequirement.IsPendingForCurrentBoot(targetUserSid);

            Title = "Undefined SS · Services Prechecker";
            Width = 1140;
            Height = 840;
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

            ImageSource icon = WindowIconLoader.LoadLargestFrame(
                Assembly.GetExecutingAssembly(),
                "UndefinedSS.ServicesPrechecker.Assets.app.ico");
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
                Text = "正在检查取证数据源…",
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
            enableButton = CreateButton("一键启用全部数据源", true);
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
                Rows = 0
            };
            ScrollViewer servicesScroll = new ScrollViewer
            {
                Content = servicesGrid,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = new Thickness(0, 0, 6, 0),
                PanningMode = PanningMode.VerticalOnly,
                Focusable = true
            };
            servicesScroll.Resources.Add(
                typeof(ScrollBar),
                BuildForensicScrollBarStyle());
            AutomationProperties.SetName(
                servicesScroll,
                "取证条件列表");
            AutomationProperties.SetHelpText(
                servicesScroll,
                "可使用鼠标滚轮、拖动滑块、方向键或 Page Up 和 Page Down 浏览全部取证条件");
            Grid.SetRow(servicesScroll, 4);
            content.Children.Add(servicesScroll);

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
            footerLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            activityText = new TextBlock
            {
                Text = "服务、取证记录源与 HWID 均在本机检查；不会上传取证数据。",
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(SecondaryTextColor),
                FontSize = 10,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            hardwareIdText = new TextBlock
            {
                Text = "点击复制 · HWID  正在读取…",
                FontFamily = new FontFamily("Consolas"),
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(SecondaryTextColor),
                VerticalAlignment = VerticalAlignment.Center
            };
            hardwareIdButton = new Button
            {
                Content = hardwareIdText,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(4, 2, 4, 2),
                Margin = new Thickness(18, 0, 18, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = Cursors.Arrow,
                IsEnabled = false,
                Template = BuildFooterTextButtonTemplate(),
                ToolTip = "正在生成 HWID…"
            };
            AutomationProperties.SetName(hardwareIdButton, "HWID 正在读取");
            ToolTipService.SetShowOnDisabled(hardwareIdButton, true);
            hardwareIdButton.Click += HandleHardwareIdClick;
            hardwareIdButton.KeyDown += HandleHardwareIdKeyDown;
            hardwareIdButton.MouseEnter +=
                delegate
                {
                    if (hardwareIdButton.IsEnabled)
                    {
                        hardwareIdText.Foreground = new SolidColorBrush(PrimaryTextColor);
                    }
                };
            hardwareIdButton.MouseLeave +=
                delegate
                {
                    if (hardwareIdButton.IsEnabled)
                    {
                        hardwareIdText.Foreground = new SolidColorBrush(AccentColor);
                    }
                };
            TextBlock versionText = new TextBlock
            {
                Text = "v" + ApplicationVersion + "  ·  FORENSICS READINESS",
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                Foreground = new SolidColorBrush(AccentColor),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 9,
                FontWeight = FontWeights.SemiBold
            };
            footerLayout.Children.Add(activityText);
            Grid.SetColumn(hardwareIdButton, 1);
            footerLayout.Children.Add(hardwareIdButton);
            Grid.SetColumn(versionText, 2);
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

            modalPanel = new Border
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
            modalEyebrow = new TextBlock
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
            modalStrong = new TextBlock
            {
                Text = "请重启系统后，再开始下一次查端。",
                Margin = new Thickness(0, 13, 0, 0),
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(RestartColor)
            };
            StackPanel modalActions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 22, 0, 0)
            };
            modalSecondaryButton = CreateButton("稍后", false);
            modalSecondaryButton.Margin = new Thickness(0, 0, 10, 0);
            modalSecondaryButton.Visibility = Visibility.Collapsed;
            modalSecondaryButton.Click += HandleModalSecondaryClick;
            modalConfirmButton = CreateButton("我知道了", true);
            modalConfirmButton.Click += HandleModalConfirmClick;
            modalActions.Children.Add(modalSecondaryButton);
            modalActions.Children.Add(modalConfirmButton);
            modalContent.Children.Add(modalEyebrow);
            modalContent.Children.Add(modalTitle);
            modalContent.Children.Add(modalBody);
            modalContent.Children.Add(modalStrong);
            modalContent.Children.Add(modalActions);
            modalPanel.Child = modalContent;
            modalLayer.Children.Add(modalPanel);

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
                Text = "WINDOWS  ·  " + ApplicationVersion,
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
            if (!autoEnable)
            {
                LaunchTelemetry.RecordInBackground(ApplicationVersion);
            }

            Task<UpdateCheckResult> updateCheckTask = autoEnable
                ? null
                : UpdateChecker.CheckForUpdateAsync(ApplicationVersion);
            Task hardwareIdLoadTask = LoadHardwareIdAsync();
            await RefreshSnapshots();
            if (autoEnable)
            {
                await EnableAllServices();
            }
            else if (restartPending)
            {
                ShowRestartModal(-1);
            }

            await hardwareIdLoadTask;

            if (updateCheckTask != null)
            {
                UpdateCheckResult updateResult = null;
                try
                {
                    updateResult = await updateCheckTask;
                }
                catch
                {
                    // Version checking is optional and must never affect readiness checks.
                }

                if (IsLoaded && IsVisible &&
                    updateResult != null && updateResult.IsUpdateAvailable)
                {
                    ShowUpdateModal(updateResult);
                }
            }
        }

        private async Task LoadHardwareIdAsync()
        {
            Task<HardwareIdResult> readTask;
            try
            {
                readTask = HardwareIdProvider.GetHardwareIdAsync();
            }
            catch
            {
                SetHardwareIdUnavailable();
                return;
            }

            HardwareIdResult result;
            try
            {
                result = await readTask;
            }
            catch
            {
                SetHardwareIdUnavailable();
                return;
            }

            if (result == null || !result.IsAvailable || string.IsNullOrWhiteSpace(result.Value))
            {
                SetHardwareIdUnavailable();
                return;
            }

            hardwareIdValue = result.Value;
            hardwareIdButton.IsEnabled = true;
            hardwareIdButton.Cursor = Cursors.Hand;
            RestoreHardwareIdText();
        }

        private void SetHardwareIdUnavailable()
        {
            hardwareIdValue = null;
            hardwareIdButton.IsEnabled = false;
            hardwareIdButton.Cursor = Cursors.Arrow;
            hardwareIdButton.ToolTip = "未能读取稳定的设备标识";
            hardwareIdText.Text = "HWID  无法读取";
            hardwareIdText.Foreground = new SolidColorBrush(SecondaryTextColor);
            AutomationProperties.SetName(hardwareIdButton, "HWID 无法读取");
            AutomationProperties.SetHelpText(
                hardwareIdButton,
                "未能读取稳定的设备标识");
        }

        private void RestoreHardwareIdText()
        {
            if (string.IsNullOrWhiteSpace(hardwareIdValue))
            {
                return;
            }

            hardwareIdText.Text = "点击复制 · HWID  " + hardwareIdValue;
            hardwareIdText.Foreground = new SolidColorBrush(AccentColor);
            hardwareIdButton.ToolTip = "点击复制 HWID";
            AutomationProperties.SetName(
                hardwareIdButton,
                "HWID " + hardwareIdValue + "，点击复制");
            AutomationProperties.SetHelpText(
                hardwareIdButton,
                "点击后将 HWID 复制到剪贴板");
        }

        private async void HandleHardwareIdClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(hardwareIdValue) || isHardwareIdCopying)
            {
                return;
            }

            isHardwareIdCopying = true;
            hardwareIdButton.IsEnabled = false;
            bool copied;
            try
            {
                copied = await ClipboardWriter.TrySetUnicodeTextWithRetryAsync(
                    hardwareIdValue);
            }
            finally
            {
                isHardwareIdCopying = false;
                hardwareIdButton.IsEnabled =
                    !string.IsNullOrWhiteSpace(hardwareIdValue);
            }

            int feedbackGeneration = ++hardwareIdFeedbackGeneration;
            if (copied)
            {
                hardwareIdText.Text = "已复制 · HWID  " + hardwareIdValue;
                hardwareIdText.Foreground = new SolidColorBrush(
                    Color.FromRgb(113, 202, 146));
                AutomationProperties.SetName(
                    hardwareIdButton,
                    "HWID 已复制");
            }
            else
            {
                hardwareIdText.Text = "复制失败 · 点击重试";
                hardwareIdText.Foreground = new SolidColorBrush(RestartColor);
                AutomationProperties.SetName(
                    hardwareIdButton,
                    "HWID 复制失败，点击重试");
            }

            await Task.Delay(copied ? 1500 : 2000);
            if (feedbackGeneration == hardwareIdFeedbackGeneration)
            {
                RestoreHardwareIdText();
            }
        }

        private void HandleHardwareIdKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter || !hardwareIdButton.IsEnabled)
            {
                return;
            }

            e.Handled = true;
            hardwareIdButton.RaiseEvent(
                new RoutedEventArgs(Button.ClickEvent, hardwareIdButton));
        }

        private async void HandleRefreshClick(object sender, RoutedEventArgs e)
        {
            restartPending =
                RestartRequirement.IsPendingForCurrentBoot(targetUserSid);
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
                    ServiceManager.RelaunchElevated(targetUserSid);
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

            SetBusy(true, "正在读取 7 项系统服务与 3 项取证记录源…");
            ReadinessSnapshotBundle snapshots;
            try
            {
                snapshots = await Task.Run(
                    delegate
                    {
                        return ReadAllSnapshots();
                    });
            }
            catch (Exception exception)
            {
                SetBusy(false, "检测失败：" + exception.Message);
                return;
            }

            RenderSnapshots(snapshots.Services, snapshots.Artifacts);
            string completedMessage = restartPending
                ? "取证数据源设置已改变：请重启系统；当前启动周期的查端仍按异常处理。"
                : "检查完成。所有检查均在本机完成，不会上传系统服务、记录状态或 HWID。";
            SetBusy(false, completedMessage);
        }

        private async Task EnableAllServices()
        {
            if (isBusy || !ServiceManager.IsAdministrator())
            {
                return;
            }

            SetBusy(true, "正在配置取证所需服务、策略与系统任务，请稍候…");

            ReadinessEnableBundle results;
            try
            {
                results = await Task.Run(
                    delegate
                    {
                        ForensicArtifactManager artifactManager =
                            ForensicArtifactManager.CreateProduction();
                        return new ReadinessEnableBundle
                        {
                            Services = ServiceManager.EnableAll(),
                            Artifacts = artifactManager.EnableAll(targetUserSid)
                        };
                    });
            }
            catch (Exception exception)
            {
                SetBusy(false, "启用失败：" + exception.Message);
                return;
            }

            bool restartRequiredByOperation =
                ServiceManager.RequiresRestartAfterEnable(results.Services) ||
                ForensicArtifactManager.RequiresRestartAfterEnable(
                    results.Artifacts);
            bool restartMarkerStored = true;
            if (restartRequiredByOperation)
            {
                restartMarkerStored =
                    RestartRequirement.MarkPendingForCurrentBoot(targetUserSid);
                restartPending = true;
            }

            ReadinessSnapshotBundle snapshots = null;
            string snapshotError = null;
            try
            {
                snapshots = await Task.Run(
                    delegate
                    {
                        return ReadAllSnapshots();
                    });
            }
            catch (Exception exception)
            {
                snapshotError = exception.Message;
            }

            if (snapshots != null)
            {
                RenderSnapshots(snapshots.Services, snapshots.Artifacts);
            }
            UpdateRestartNotice();

            int failedServices = results.Services.Count(
                delegate(EnableResult item) { return !item.Success; });
            int failedArtifacts = results.Artifacts.Count(
                delegate(ForensicArtifactEnableResult item)
                {
                    return !item.Success;
                });
            int failed = failedServices + failedArtifacts;
            string message;
            if (!restartRequiredByOperation && !restartPending && failed == 0)
            {
                message = "全部取证数据源已处于正确状态，本次操作未作更改，无需重启。";
            }
            else if (!restartRequiredByOperation && restartPending && failed == 0)
            {
                message = "全部取证数据源当前已就绪，但本次启动周期仍无效；必须重启电脑。";
            }
            else if (failed == 0)
            {
                message = "全部可用取证数据源已配置；必须重启电脑，后续查端才可生效。";
            }
            else
            {
                IEnumerable<string> failedServiceNames =
                    results.Services
                        .Where(delegate(EnableResult item) { return !item.Success; })
                        .Select(delegate(EnableResult item)
                        {
                            return item.Definition.DisplayName;
                        });
                IEnumerable<string> failedArtifactNames =
                    results.Artifacts
                        .Where(delegate(ForensicArtifactEnableResult item)
                        {
                            return !item.Success;
                        })
                        .Select(delegate(ForensicArtifactEnableResult item)
                        {
                            return item.DisplayName;
                        });
                string failedNames = string.Join(
                    "、",
                    failedServiceNames.Concat(failedArtifactNames));
                message =
                    "部分数据源已配置，但 " + failedNames +
                    " 未能完整启用。缺失的 Windows 组件不会被擅自创建；处理后仍须重启。";
            }

            if (restartRequiredByOperation && !restartMarkerStored)
            {
                message += " 未能为启动本程序的用户保存重启标记，请务必手动重启。";
            }

            if (!string.IsNullOrWhiteSpace(snapshotError))
            {
                message += " 数据源复检失败：" + snapshotError +
                    "；重启要求仍然有效。";
            }

            SetBusy(false, message);
            if (restartPending)
            {
                ShowRestartModal(restartRequiredByOperation ? failed : -1);
            }
        }

        private ReadinessSnapshotBundle ReadAllSnapshots()
        {
            ForensicArtifactManager artifactManager =
                ForensicArtifactManager.CreateProduction();
            return new ReadinessSnapshotBundle
            {
                Services = ServiceManager.GetSnapshots(),
                Artifacts = artifactManager.GetSnapshots(targetUserSid)
            };
        }

        private void RenderSnapshots(
            IList<ServiceSnapshot> serviceSnapshots,
            IList<ForensicArtifactSnapshot> artifactSnapshots)
        {
            if (restartPending)
            {
                foreach (ServiceSnapshot snapshot in serviceSnapshots)
                {
                    if (snapshot.VisualState == ServiceVisualState.Running)
                    {
                        snapshot.VisualState = ServiceVisualState.RebootRequired;
                        snapshot.StatusText = "等待重启";
                        snapshot.Detail = "服务已运行，但仅在重启后的查端中生效";
                    }
                }
                foreach (ForensicArtifactSnapshot snapshot in artifactSnapshots)
                {
                    if (snapshot.VisualState == ServiceVisualState.Running)
                    {
                        snapshot.VisualState = ServiceVisualState.RebootRequired;
                        snapshot.StatusText = "等待重启";
                        snapshot.Detail = "记录机制已配置，但仅在重启后的查端中作为有效基线";
                    }
                }
            }

            servicesGrid.Children.Clear();
            foreach (ServiceSnapshot snapshot in serviceSnapshots)
            {
                servicesGrid.Children.Add(BuildServiceCard(snapshot));
            }
            foreach (ForensicArtifactSnapshot snapshot in artifactSnapshots)
            {
                servicesGrid.Children.Add(BuildArtifactCard(snapshot));
            }
            servicesGrid.Children.Add(BuildInformationCard());

            int healthyServices = serviceSnapshots.Count(
                delegate(ServiceSnapshot snapshot)
                {
                    return snapshot.VisualState == ServiceVisualState.Running;
                });
            int healthyArtifacts = artifactSnapshots.Count(
                delegate(ForensicArtifactSnapshot snapshot)
                {
                    return snapshot.VisualState == ServiceVisualState.Running;
                });
            int attentionServices = serviceSnapshots.Count(
                delegate(ServiceSnapshot snapshot)
                {
                    return snapshot.VisualState != ServiceVisualState.Running;
                });
            int attentionArtifacts = artifactSnapshots.Count(
                delegate(ForensicArtifactSnapshot snapshot)
                {
                    return snapshot.VisualState != ServiceVisualState.Running;
                });
            int healthy = healthyServices + healthyArtifacts;
            int total = serviceSnapshots.Count + artifactSnapshots.Count;
            int attention = attentionServices + attentionArtifacts;

            if (restartPending)
            {
                summaryText.Text = "服务已配置，等待系统重启";
                summaryText.Foreground = new SolidColorBrush(RestartColor);
            }
            else if (healthy == total)
            {
                summaryText.Text = total + " / " + total + " 项取证条件正常";
                summaryText.Foreground = new SolidColorBrush(Color.FromRgb(119, 205, 151));
            }
            else
            {
                summaryText.Text = healthy + " / " + total + " 项取证条件正常";
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
                    (attention > 0 ? "  ·  " + attention + " 项需要处理" : "  ·  已具备取证记录条件");
            }
        }

        private FrameworkElement BuildServiceCard(ServiceSnapshot snapshot)
        {
            string detail = string.IsNullOrWhiteSpace(snapshot.Detail)
                ? "启动方式：" + snapshot.StartTypeText
                : snapshot.Detail;
            return BuildReadinessCard(
                snapshot.Definition.DisplayName,
                snapshot.Definition.ServiceName,
                snapshot.Definition.Description,
                detail,
                snapshot.VisualState,
                snapshot.StatusText);
        }

        private FrameworkElement BuildArtifactCard(
            ForensicArtifactSnapshot snapshot)
        {
            return BuildReadinessCard(
                snapshot.DisplayName,
                snapshot.CodeName,
                snapshot.Description,
                snapshot.Detail,
                snapshot.VisualState,
                snapshot.StatusText);
        }

        private FrameworkElement BuildReadinessCard(
            string displayName,
            string codeName,
            string descriptionText,
            string detailText,
            ServiceVisualState state,
            string statusText)
        {
            Border card = new Border
            {
                Background = new SolidColorBrush(PanelColor),
                BorderBrush = new SolidColorBrush(BorderColor),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Margin = new Thickness(5),
                Padding = new Thickness(14, 12, 14, 11),
                MinHeight = 128
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
                Text = displayName,
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
                Text = codeName,
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
                Text = descriptionText,
                Foreground = new SolidColorBrush(SecondaryTextColor),
                FontSize = 10,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 6)
            };
            Grid.SetRow(description, 2);
            cardGrid.Children.Add(description);

            TextBlock detail = new TextBlock
            {
                Text = detailText,
                Foreground = new SolidColorBrush(Color.FromRgb(123, 120, 114)),
                FontSize = 9,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Top
            };
            Grid.SetRow(detail, 3);
            cardGrid.Children.Add(detail);

            Border statePill = BuildStatePill(state, statusText);
            Grid.SetRow(statePill, 4);
            cardGrid.Children.Add(statePill);

            card.Child = cardGrid;
            return card;
        }

        private Border BuildStatePill(ServiceSnapshot snapshot)
        {
            return BuildStatePill(snapshot.VisualState, snapshot.StatusText);
        }

        private Border BuildStatePill(
            ServiceVisualState state,
            string statusText)
        {
            Color stateColor = GetStateColor(state);
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
                Text = statusText,
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
            border.Name = "focusBorder";
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

            Trigger focusTrigger = new Trigger
            {
                Property = Button.IsKeyboardFocusedProperty,
                Value = true
            };
            focusTrigger.Setters.Add(
                new Setter(
                    Border.BorderBrushProperty,
                    new SolidColorBrush(RestartColor),
                    "focusBorder"));
            focusTrigger.Setters.Add(
                new Setter(
                    Border.BorderThicknessProperty,
                    new Thickness(2),
                    "focusBorder"));
            template.Triggers.Add(focusTrigger);
            return template;
        }

        private static Style BuildForensicScrollBarStyle()
        {
            const string xaml =
                @"<Style xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
                         xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
                         TargetType=""{x:Type ScrollBar}"">
                    <Setter Property=""Width"" Value=""12"" />
                    <Setter Property=""MinWidth"" Value=""12"" />
                    <Setter Property=""Background"" Value=""Transparent"" />
                    <Setter Property=""FocusVisualStyle"" Value=""{x:Null}"" />
                    <Setter Property=""Opacity"" Value=""0.76"" />
                    <Setter Property=""Template"">
                        <Setter.Value>
                            <ControlTemplate TargetType=""{x:Type ScrollBar}"">
                                <Border x:Name=""Rail""
                                        Margin=""1,0""
                                        Background=""#0B0C0C""
                                        BorderBrush=""#2B2824""
                                        BorderThickness=""1""
                                        CornerRadius=""6""
                                        SnapsToDevicePixels=""True"">
                                    <Track x:Name=""PART_Track""
                                           Margin=""2""
                                           Orientation=""Vertical""
                                           IsDirectionReversed=""True""
                                           Minimum=""{TemplateBinding Minimum}""
                                           Maximum=""{TemplateBinding Maximum}""
                                           Value=""{TemplateBinding Value}""
                                           ViewportSize=""{TemplateBinding ViewportSize}"">
                                        <Track.DecreaseRepeatButton>
                                            <RepeatButton Command=""{x:Static ScrollBar.PageUpCommand}""
                                                          Focusable=""False""
                                                          IsTabStop=""False""
                                                          Background=""Transparent"">
                                                <RepeatButton.Template>
                                                    <ControlTemplate TargetType=""{x:Type RepeatButton}"">
                                                        <Border Background=""Transparent"" />
                                                    </ControlTemplate>
                                                </RepeatButton.Template>
                                            </RepeatButton>
                                        </Track.DecreaseRepeatButton>
                                        <Track.Thumb>
                                            <Thumb MinHeight=""38"" MinWidth=""8"" Cursor=""SizeNS"">
                                                <Thumb.Template>
                                                    <ControlTemplate TargetType=""{x:Type Thumb}"">
                                                        <Border x:Name=""ThumbSurface""
                                                                Width=""5""
                                                                HorizontalAlignment=""Center""
                                                                Background=""#6F6557""
                                                                BorderBrush=""#897D6B""
                                                                BorderThickness=""1""
                                                                CornerRadius=""3""
                                                                SnapsToDevicePixels=""True"" />
                                                        <ControlTemplate.Triggers>
                                                            <Trigger Property=""IsMouseOver"" Value=""True"">
                                                                <Setter TargetName=""ThumbSurface"" Property=""Width"" Value=""7"" />
                                                                <Setter TargetName=""ThumbSurface"" Property=""Background"" Value=""#A39681"" />
                                                            </Trigger>
                                                            <Trigger Property=""IsDragging"" Value=""True"">
                                                                <Setter TargetName=""ThumbSurface"" Property=""Width"" Value=""7"" />
                                                                <Setter TargetName=""ThumbSurface"" Property=""Background"" Value=""#E79E4C"" />
                                                                <Setter TargetName=""ThumbSurface"" Property=""BorderBrush"" Value=""#F4C477"" />
                                                            </Trigger>
                                                        </ControlTemplate.Triggers>
                                                    </ControlTemplate>
                                                </Thumb.Template>
                                            </Thumb>
                                        </Track.Thumb>
                                        <Track.IncreaseRepeatButton>
                                            <RepeatButton Command=""{x:Static ScrollBar.PageDownCommand}""
                                                          Focusable=""False""
                                                          IsTabStop=""False""
                                                          Background=""Transparent"">
                                                <RepeatButton.Template>
                                                    <ControlTemplate TargetType=""{x:Type RepeatButton}"">
                                                        <Border Background=""Transparent"" />
                                                    </ControlTemplate>
                                                </RepeatButton.Template>
                                            </RepeatButton>
                                        </Track.IncreaseRepeatButton>
                                    </Track>
                                </Border>
                                <ControlTemplate.Triggers>
                                    <Trigger Property=""IsMouseOver"" Value=""True"">
                                        <Setter TargetName=""Rail"" Property=""BorderBrush"" Value=""#524B41"" />
                                    </Trigger>
                                    <Trigger Property=""IsKeyboardFocusWithin"" Value=""True"">
                                        <Setter TargetName=""Rail"" Property=""BorderBrush"" Value=""#E79E4C"" />
                                    </Trigger>
                                </ControlTemplate.Triggers>
                            </ControlTemplate>
                        </Setter.Value>
                    </Setter>
                    <Style.Triggers>
                        <Trigger Property=""IsMouseOver"" Value=""True"">
                            <Setter Property=""Opacity"" Value=""1"" />
                        </Trigger>
                        <Trigger Property=""IsKeyboardFocusWithin"" Value=""True"">
                            <Setter Property=""Opacity"" Value=""1"" />
                        </Trigger>
                        <Trigger Property=""IsEnabled"" Value=""False"">
                            <Setter Property=""Opacity"" Value=""0.3"" />
                        </Trigger>
                    </Style.Triggers>
                </Style>";
            return (Style)XamlReader.Parse(xaml);
        }

        private static ControlTemplate BuildFooterTextButtonTemplate()
        {
            FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border));
            border.Name = "focusBorder";
            border.SetValue(Border.BackgroundProperty, Brushes.Transparent);
            border.SetValue(Border.BorderBrushProperty, Brushes.Transparent);
            border.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(3));
            border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Button.PaddingProperty));

            FrameworkElementFactory presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(
                ContentPresenter.HorizontalAlignmentProperty,
                HorizontalAlignment.Center);
            presenter.SetValue(
                ContentPresenter.VerticalAlignmentProperty,
                VerticalAlignment.Center);
            border.AppendChild(presenter);

            ControlTemplate template = new ControlTemplate(typeof(Button));
            template.VisualTree = border;

            Trigger focusTrigger = new Trigger
            {
                Property = Button.IsKeyboardFocusedProperty,
                Value = true
            };
            focusTrigger.Setters.Add(
                new Setter(
                    Border.BorderBrushProperty,
                    new SolidColorBrush(Color.FromRgb(111, 101, 87)),
                    "focusBorder"));
            template.Triggers.Add(focusTrigger);

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
                restartNoticeTitle.Text = "已配置取证数据源，必须重启系统";
                restartNoticeBody.Text =
                    "当前启动周期内的查端仍按异常处理。请重启 Windows；只有重启后的后续查端才有效。";
            }
            else
            {
                restartNotice.Background = new SolidColorBrush(Color.FromRgb(22, 18, 14));
                restartNotice.BorderBrush = new SolidColorBrush(Color.FromRgb(79, 59, 39));
                restartNoticeTitle.Text = "启用任何所需数据源后，都必须重启系统";
                restartNoticeBody.Text =
                    "如果现在才启用服务，本次查端仍按异常处理；只有重启后的后续查端才有效。";
            }
        }

        private void ShowRestartModal(int failedCount)
        {
            if (modalLayer.Visibility == Visibility.Visible &&
                modalPurpose == ModalPurpose.Update &&
                activeUpdateResult != null)
            {
                pendingUpdateResult = activeUpdateResult;
            }

            PrepareModal(ModalPurpose.Restart);
            activeUpdateResult = null;
            modalPurpose = ModalPurpose.Restart;
            modalPanel.BorderBrush = new SolidColorBrush(Color.FromRgb(104, 82, 57));
            modalEyebrow.Text = "RESTART REQUIRED";
            modalEyebrow.Foreground = new SolidColorBrush(RestartColor);
            modalStrong.Text = "请重启系统后，再开始下一次查端。";
            modalStrong.Foreground = new SolidColorBrush(RestartColor);
            modalStrong.Visibility = Visibility.Visible;
            modalSecondaryButton.Visibility = Visibility.Collapsed;
            modalConfirmButton.Content = "我知道了";
            AutomationProperties.SetName(modalPanel, "必须重启系统");
            AutomationProperties.SetHelpText(
                modalPanel,
                "当前启动周期的查端仍按异常处理，重启后的后续查端才有效");
            AutomationProperties.SetName(modalConfirmButton, "关闭重启提示");

            if (failedCount < 0)
            {
                modalTitle.Text = "仍需重启电脑";
                modalBody.Text =
                    "检测到服务设置是在本次 Windows 启动期间完成的。当前启动周期的查端仍会按照“异常”处理，不能用于后续查端判断。";
            }
            else if (failedCount == 0)
            {
                modalTitle.Text = "取证数据源已配置，需要重启电脑";
                modalBody.Text =
                    "全部可用取证数据源已完成配置。但这些设置不会让当前启动周期的查端变为有效，本次查端结果仍会按照“异常”处理。";
            }
            else
            {
                modalTitle.Text = "部分取证数据源已配置";
                modalBody.Text =
                    "已完成可用的数据源设置，但仍有项目无法完整启用。解决失败项目后也必须重启系统；当前启动周期的查端仍会按照“异常”处理。";
            }

            modalConfirmButton.Focus();
        }

        private void ShowUpdateModal(UpdateCheckResult updateResult)
        {
            if (updateResult == null || !updateResult.IsUpdateAvailable)
            {
                return;
            }

            if (modalLayer.Visibility == Visibility.Visible &&
                modalPurpose == ModalPurpose.Restart)
            {
                pendingUpdateResult = updateResult;
                return;
            }

            PrepareModal(ModalPurpose.Update);
            activeUpdateResult = updateResult;
            modalPanel.BorderBrush = new SolidColorBrush(Color.FromRgb(83, 78, 68));
            modalEyebrow.Text = "UPDATE AVAILABLE";
            modalEyebrow.Foreground = new SolidColorBrush(AccentColor);
            modalTitle.Text = "发现新版本 " + FormatVersionLabel(updateResult.LatestVersion);
            modalBody.Text =
                "当前版本为 " + FormatVersionLabel(ApplicationVersion) + "，最新版本为 " +
                FormatVersionLabel(updateResult.LatestVersion) + "。建议更新后再进行后续查端。";
            modalStrong.Text = "官方下载地址：dl.screenshare.cn";
            modalStrong.Foreground = new SolidColorBrush(AccentColor);
            modalStrong.Visibility = Visibility.Visible;
            modalSecondaryButton.Content = "稍后";
            modalSecondaryButton.Visibility = Visibility.Visible;
            modalConfirmButton.Content = "前往下载";
            AutomationProperties.SetName(modalPanel, "发现 Services Prechecker 新版本");
            AutomationProperties.SetHelpText(
                modalPanel,
                "可稍后处理，或在默认浏览器中打开 Undefined SS 官方文件下载地址");
            AutomationProperties.SetName(modalSecondaryButton, "稍后更新");
            AutomationProperties.SetName(modalConfirmButton, "前往下载最新版本");
            AutomationProperties.SetHelpText(
                modalConfirmButton,
                "在默认浏览器中打开 Undefined SS 官方文件下载地址");
            modalConfirmButton.Focus();
        }

        private void PrepareModal(ModalPurpose purpose)
        {
            if (modalLayer.Visibility != Visibility.Visible)
            {
                focusBeforeModal = Keyboard.FocusedElement;
            }

            modalPurpose = purpose;
            windowFrame.IsEnabled = false;
            modalLayer.Visibility = Visibility.Visible;
        }

        private void HandleModalConfirmClick(object sender, RoutedEventArgs e)
        {
            if (modalPurpose != ModalPurpose.Update)
            {
                DismissModal(true);
                return;
            }

            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = UpdateChecker.DownloadUrl,
                    UseShellExecute = true
                };
                Process.Start(startInfo);
                DismissModal(false);
            }
            catch (Exception exception)
            {
                if (exception is OutOfMemoryException ||
                    exception is StackOverflowException ||
                    exception is AccessViolationException)
                {
                    throw;
                }

                modalBody.Text =
                    "无法打开默认浏览器。请稍后重试，或手动访问：\n" +
                    UpdateChecker.DownloadUrl;
                modalStrong.Text = "未下载或执行任何文件。";
                modalConfirmButton.Content = "重试";
                modalConfirmButton.Focus();
            }
        }

        private void HandleModalSecondaryClick(object sender, RoutedEventArgs e)
        {
            DismissModal(false);
        }

        private void DismissModal(bool showPendingUpdate)
        {
            modalLayer.Visibility = Visibility.Collapsed;
            modalPurpose = ModalPurpose.None;
            activeUpdateResult = null;
            windowFrame.IsEnabled = true;

            IInputElement focusTarget = focusBeforeModal;
            focusBeforeModal = null;
            if (focusTarget != null)
            {
                Keyboard.Focus(focusTarget);
            }
            else
            {
                enableButton.Focus();
            }

            if (showPendingUpdate && pendingUpdateResult != null)
            {
                ShowPendingUpdateAfterDismissal();
            }
        }

        private async void ShowPendingUpdateAfterDismissal()
        {
            UpdateCheckResult updateResult = pendingUpdateResult;
            pendingUpdateResult = null;
            await Task.Delay(180);
            if (IsLoaded && IsVisible && updateResult != null)
            {
                ShowUpdateModal(updateResult);
            }
        }

        private static string FormatVersionLabel(string version)
        {
            if (string.IsNullOrWhiteSpace(version))
            {
                return "未知";
            }

            string trimmed = version.Trim();
            return trimmed.StartsWith("v", StringComparison.OrdinalIgnoreCase)
                ? trimmed
                : "v" + trimmed;
        }

        private void SetBusy(bool busy, string activity)
        {
            isBusy = busy;
            refreshButton.IsEnabled = !busy;
            enableButton.IsEnabled = !busy;
            enableButton.Content = busy ? "正在处理…" : "一键启用全部数据源";
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
                DismissModal(modalPurpose == ModalPurpose.Restart);
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

        private static string GetApplicationVersion()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            AssemblyInformationalVersionAttribute attribute =
                (AssemblyInformationalVersionAttribute)Attribute.GetCustomAttribute(
                    assembly,
                    typeof(AssemblyInformationalVersionAttribute));

            if (attribute != null &&
                !string.IsNullOrWhiteSpace(attribute.InformationalVersion))
            {
                return attribute.InformationalVersion;
            }

            Version version = assembly.GetName().Version;
            return version == null ? "0.0.0" : version.ToString(3);
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
