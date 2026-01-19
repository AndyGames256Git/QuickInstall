using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

namespace QuickInstallWPF
{
    public partial class MainWindow : Window
    {
        private readonly HttpClient httpClient = new HttpClient();

        // App categories
        private readonly Dictionary<string, List<AppInfo>> Categories = new()
        {
            ["Development"] = new List<AppInfo>
            {
                new AppInfo("Visual Studio", "https://aka.ms/vs/17/release/vs_community.exe", "https://upload.wikimedia.org/wikipedia/commons/4/4f/Visual_Studio_2022_logo.svg"),
                new AppInfo("Notepad++", "https://github.com/notepad-plus-plus/notepad-plus-plus/releases/download/v8.5.3/npp.8.5.3.Installer.x64.exe", "https://notepad-plus-plus.org/images/logo.svg"),
                new AppInfo("Git", "https://github.com/git-for-windows/git/releases/download/v2.42.0.windows.1/Git-2.42.0-64-bit.exe", "https://git-scm.com/images/logos/downloads/Git-Logo-2Color.png")
            },
            ["Games"] = new List<AppInfo>
            {
                new AppInfo("Counter-Strike 1.6", "https://dllx.down-cs.su/downloads/cs_16_clean_eng.exe", "https://upload.wikimedia.org/wikipedia/en/3/35/Counter-Strike-1.6_cover.jpg"),
                new AppInfo("Half-Life", "https://dllx.down-cs.su/downloads/half_life.exe", "https://upload.wikimedia.org/wikipedia/en/3/37/Half-Life_cover.jpg"),
                new AppInfo("Garry's Mod", "https://dllx.down-cs.su/downloads/gmod.exe", "https://upload.wikimedia.org/wikipedia/en/3/34/Garry%27s_Mod_cover.jpg"),
                new AppInfo("Half-Life 2", "https://dllx.down-cs.su/downloads/half_life_2.exe", "https://upload.wikimedia.org/wikipedia/en/2/2d/Half-Life_2_cover.jpg")
            }
        };

        public MainWindow()
        {
            InitializeComponent();
            CategoryComboBox.ItemsSource = Categories.Keys;
            CategoryComboBox.SelectedIndex = 0;
        }

        private void CategoryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            AppsPanel.Children.Clear();
            string? category = CategoryComboBox.SelectedItem as string;
            if (category == null || !Categories.ContainsKey(category)) return;

            foreach (var app in Categories[category])
            {
                AppsPanel.Children.Add(CreateAppCard(app));
            }
        }

        private Border CreateAppCard(AppInfo app)
        {
            var card = new Border
            {
                Width = 160,
                Height = 220,
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 47)),
                CornerRadius = new CornerRadius(10),
                Margin = new Thickness(10),
                Cursor = Cursors.Hand,
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 8,
                    ShadowDepth = 2
                }
            };

            var stack = new StackPanel { Margin = new Thickness(5) };

            // Box art image
            var image = new Image
            {
                Source = new BitmapImage(new Uri(app.ImageUrl)),
                Width = 140,
                Height = 140,
                Stretch = Stretch.UniformToFill,
                Margin = new Thickness(0, 0, 0, 5)
            };
            stack.Children.Add(image);

            // App name
            stack.Children.Add(new TextBlock
            {
                Text = app.Name,
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center
            });

            // Install button
            var btn = new Button
            {
                Content = "Install",
                Background = new SolidColorBrush(Color.FromRgb(76, 175, 80)),
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 5, 0, 0),
                Cursor = Cursors.Hand,
                BorderThickness = new Thickness(0)
            };

            // Hover effect: glow + lift
            btn.MouseEnter += (s, e) =>
            {
                btn.Background = new SolidColorBrush(Color.FromRgb(102, 187, 106));
                card.RenderTransform = new TranslateTransform(0, -5);
                card.Effect = new DropShadowEffect { Color = Colors.LimeGreen, BlurRadius = 15, ShadowDepth = 4 };
            };
            btn.MouseLeave += (s, e) =>
            {
                btn.Background = new SolidColorBrush(Color.FromRgb(76, 175, 80));
                card.RenderTransform = new TranslateTransform(0, 0);
                card.Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 8, ShadowDepth = 2 };
            };

            btn.Click += async (s, e) =>
            {
                await InstallApp(app);
            };

            stack.Children.Add(btn);
            card.Child = stack;
            return card;
        }

        private async Task InstallApp(AppInfo app)
        {
            string tempFile = Path.Combine(Path.GetTempPath(), app.Name + ".exe");

            StatusLabel.Text = "Status: Downloading...";
            InstallProgressBar.Value = 0;

            try
            {
                using var response = await httpClient.GetAsync(app.Url, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();
                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                var canReport = totalBytes != -1;

                using var stream = await response.Content.ReadAsStreamAsync();
                using var fs = File.Create(tempFile);

                var buffer = new byte[8192];
                long totalRead = 0;
                int read;
                while ((read = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fs.WriteAsync(buffer, 0, read);
                    totalRead += read;

                    if (canReport)
                    {
                        int progress = (int)((totalRead * 100) / totalBytes);
                        InstallProgressBar.Value = progress;
                    }
                }

                StatusLabel.Text = "Status: Installing...";
                InstallProgressBar.Value = 0;

                Process installer = new Process();
                installer.StartInfo.FileName = tempFile;
                installer.StartInfo.UseShellExecute = true;
                installer.EnableRaisingEvents = true;

                installer.Exited += (s, ev) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        StatusLabel.Text = $"Status: {app.Name} Installation Complete!";
                        InstallProgressBar.Value = 100;
                    });
                };

                installer.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message);
            }
        }
    }

    // **AppInfo class definition**
    public class AppInfo
    {
        public string Name { get; set; }
        public string Url { get; set; }
        public string ImageUrl { get; set; }

        public AppInfo(string name, string url, string imageUrl)
        {
            Name = name;
            Url = url;
            ImageUrl = imageUrl;
        }
    }
}
