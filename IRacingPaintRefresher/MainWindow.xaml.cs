global using System.IO;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Documents;

namespace IRacingPaintRefresher
{
    public enum ImageType
    {
        Paint,
        SpecMap
    }

    public partial class MainWindow : Window
    {
        private const string AcceptedImageFormats = "*.psd;*.pdn;*.svg;";
        private MD5 md5 = MD5.Create();

        private volatile string? paintFilePath = null;
        private volatile string? specMapFilePath = null;
        private volatile bool WindowOpen = true;
        private volatile string paintFileHash = string.Empty;
        private volatile string specMapFileHash = string.Empty;

        public Thread? ImageListener = null;

        public MainWindow()
        {
            try
            {
                InitializeComponent();
                ResizeMode = ResizeMode.NoResize;
                TextBox_Log.IsReadOnly = true;
                LoadConfiguration();
                StartListeners();
            }
            catch(Exception e)
            {
                string errorMessage = $"Error while loading\r\n{e.Message}";
                MessageBox.Show(errorMessage, "Fatal error", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        }


        private void LoadConfiguration()
        {
            AppConfig.Load();
            TextBox_IRacingId.Text = $"{AppConfig.IRacingId}";
            if(string.IsNullOrEmpty(AppConfig.OutputPath))
            {
                AppConfig.OutputPath = Directory.GetCurrentDirectory();
            }
            TextBox_OutputPath.Content = AppConfig.OutputPath;
            DebugLogMessage("Config loaded");
        }


        private void StartListeners()
        {
            ImageListener = new(ListenForImages)
            {
                IsBackground = true
            };
            ImageListener.Start();
            DebugLogMessage("Listeners started");
        }


        private void ListenForImages()
        {
            DateTime? paintLastConversion = null;
            DateTime? specLastConversion = null;
            while(WindowOpen)
            {
                int iRacingId = AppConfig.IRacingId.GetValueOrDefault();
                try
                {
                    Dispatcher.Invoke(new Action(() =>
                    {
                        iRacingId = int.Parse(TextBox_IRacingId.Text);
                    }));
                }
                catch
                {
                    iRacingId = AppConfig.IRacingId.GetValueOrDefault();
                }
                AppConfig.IRacingId = iRacingId;
                try
                {
                    if (!string.IsNullOrEmpty(paintFilePath)
                        && File.Exists(paintFilePath))
                    {
                        FileInfo fileInfo = new(paintFilePath);
                        if(paintLastConversion == null || paintLastConversion < fileInfo.LastWriteTime)
                        {
                            string hash = GetFileChecksum(paintFilePath);
                            if(paintFileHash != hash)
                            {
                                ConvertPaint();
                            }
                            paintFileHash = hash;
                            paintLastConversion = DateTime.Now;
                        }
                    }
                    if (!string.IsNullOrEmpty(specMapFilePath)
                        && File.Exists(specMapFilePath))
                    {
                        FileInfo fileInfo = new(specMapFilePath);
                        if (specLastConversion == null || specLastConversion < fileInfo.LastWriteTime)
                        {
                            string hash = GetFileChecksum(specMapFilePath);
                            if(specMapFileHash != hash)
                            {
                                ConvertSpecMap();
                            }
                            specMapFileHash = hash;
                            specLastConversion = DateTime.Now;
                        }
                    }
                }
                catch { }
                Thread.Sleep(500);
            }
        }


        #region Image Conversion methods


        private void ConvertPaint()
        {
            if (!string.IsNullOrEmpty(paintFilePath))
            {
                try
                {
                    ImageConverter converter = new(ImageType.Paint);
                    converter.ConvertImage(paintFilePath);
                    LogMessage("Paint file refreshed");
                }
                catch(Exception e)
                {
                    LogMessage($"Error converting paint: {e.Message}");
                    DebugLogMessage(e.StackTrace);
                }
            }
        }


        private void ConvertSpecMap()
        {
            if (!string.IsNullOrEmpty(specMapFilePath))
            {
                try
                {
                    ImageConverter converter = new(ImageType.SpecMap);
                    converter.ConvertImage(specMapFilePath);
                    LogMessage("Spec map file refreshed");
                }
                catch (Exception e)
                {
                    LogMessage($"Error converting spec map: {e.Message}");
                }
            }
        }

        #endregion


        #region Event Listeners

        private void OnWindowClose(object sender, System.ComponentModel.CancelEventArgs e)
        {
            WindowOpen = false;
        }


        private void OnLoadPaintPath(object sender, RoutedEventArgs e)
        {
            string? paintFilePath = LoadImageFile("Load Paint");
            if (paintFilePath == null)
                return;

            this.paintFilePath = paintFilePath;
            string? path = Path.GetDirectoryName(this.paintFilePath);
            SetOutputPath(path);
            Label_PaintFilePath.Content = this.paintFilePath;
            ConvertPaint();
        }


        private void OnLoadSpecMapPath(object sender, RoutedEventArgs e)
        {
            string? specMapFilePath = LoadImageFile("Load Paint");
            if (specMapFilePath == null)
                return;

            this.specMapFilePath = specMapFilePath;
            Label_SpecMapFilePath.Content = this.specMapFilePath;
            ConvertSpecMap();
        }


        private void OnSetOutputPath(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog folderDialog = new();
            var dialogResult = folderDialog.ShowDialog();
            if (dialogResult != System.Windows.Forms.DialogResult.OK)
                return;

            if (!Directory.Exists(folderDialog.SelectedPath))
                return;

            AppConfig.OutputPath = folderDialog.SelectedPath;
            TextBox_OutputPath.Content = AppConfig.OutputPath;
            OnRefreshPaint(sender, e);
        }


        private void OnOpenOutputPath(object sender, RoutedEventArgs e)
        {
            if(string.IsNullOrEmpty(AppConfig.OutputPath))
            {
                return;
            }

            Process.Start("explorer.exe", AppConfig.OutputPath);
        }


        private void OnCustomNumberChange(object sender, RoutedEventArgs e)
        {
            AppConfig.IsCustomNumber = Checkbox_CustomNumber.IsChecked.GetValueOrDefault();
            if(false == AppConfig.IsCustomNumber)
            {
                string filePath = Path.Combine(AppConfig.OutputPath, $"car_num_{AppConfig.IRacingId}.tga");
                if(File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            OnRefreshPaint(sender, e);
            OnRefreshSpecMap(sender, e);
        }


        private void OnRefreshPaint(object sender, RoutedEventArgs e)
        {
            ConvertPaint();
        }


        private void OnRefreshSpecMap(object sender, RoutedEventArgs e)
        {
            ConvertSpecMap();
        }

        private void OnDownloadTemplates(object sender, RoutedEventArgs e)
        {
            LogMessage("Downloading iRacing templates...");
            Button_DownloadTemplates.IsEnabled = false;
            Button_DownloadTemplates.Content = "Downloading...";
            Thread t = new(DownloadTemplates);
            t.Start();
        }

        #endregion


        #region Helpers


        private void SetOutputPath(string? path)
        {
            ArgumentNullException.ThrowIfNull(path, nameof(path));
            string[] files = Directory.GetFiles(path, "paintconfig.json");
            if (files.Length == 0)
            {
                string parentPath = Directory.GetParent(path).FullName;
                files = Directory.GetFiles(parentPath, "paintconfig.json");
            }
            if (files.Length == 0)
            {
                AppConfig.OutputPath = path;
                TextBox_OutputPath.Content = path;
                LogMessage($"Output path set to {path}");
                return;
            }

            try
            {
                string jsonFileContent = File.ReadAllText(files[0]);
                var pathConfig = JsonSerializer.Deserialize<IRacingPaintPathConfig>(jsonFileContent);
                string? newPath = pathConfig.IRacingPath;
                if (false == string.IsNullOrEmpty(newPath)
                    && Directory.Exists(newPath))
                {
                    path = newPath;
                }
            }
            catch { }
            AppConfig.OutputPath = path;
            TextBox_OutputPath.Content = path;
            LogMessage($"Output path set to {path}");
        }


        private static string? LoadImageFile(string? title)
        {
            OpenFileDialog fileDialog = new()
            {
                Title = title,
                Multiselect = false,
                Filter = $"Image Files|{AcceptedImageFormats}"
            };

            bool dialogResult = fileDialog.ShowDialog().GetValueOrDefault();
            if (!dialogResult)
            {
                return null;
            }

            string fileName = fileDialog.FileName;
            return File.Exists(fileName)
                ? fileName
                : null; // How do we reach here?
        }


        private void DebugLogMessage(string? message)
        {
            #if DEBUG
            LogMessage(message);
            #endif
        }


        private bool LogMessage(string? message)
        {
            if(string.IsNullOrEmpty(message))
            {
                return false;
            }

            Dispatcher.Invoke(new Action(() =>
            {
                Paragraph paragraph = new()
                {
                    Margin = new(0),
                    Padding = new(0)
                };
                DateTime currentTime = DateTime.Now;
                string timestamp = $"[{currentTime.Hour:00}:{currentTime.Minute:00}:{currentTime.Second:00}.{currentTime.Millisecond:000}] ";
                message = $"{timestamp} {message}";
                paragraph.Inlines.Add(message);
                TextBox_Log.Document.Blocks.Add(paragraph);
            }));
            return true;
        }


        private string GetFileChecksum(string filePath)
        {
            using var stream = File.OpenRead(filePath);
            byte[] hash = md5.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }


        private async void DownloadTemplates()
        {
            try
            {
                await TradingPaintsDownloader.DownloadTemplates(e => LogMessage(e));
                LogMessage("Download complete");
            }
            catch (Exception ex)
            {
                LogMessage($"Download failed: {ex.Message}");
            }
            finally
            {
                Dispatcher.Invoke(new Action(() =>
                {
                    Button_DownloadTemplates.IsEnabled = true;
                    Button_DownloadTemplates.Content = "Download Templates";
                }));
            }
        }

        #endregion
    }
}


internal class IRacingPaintPathConfig
{
    public string? IRacingPath { get; set; }
}