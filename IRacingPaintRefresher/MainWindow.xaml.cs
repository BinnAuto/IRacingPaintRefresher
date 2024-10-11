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
            TextBox_IRacingId.Text = $"{AppConfig.iRacingId}";
            if(string.IsNullOrEmpty(AppConfig.OutputPath))
            {
                AppConfig.OutputPath = Directory.GetCurrentDirectory();
            }
            TextBox_OutputPath.Content = AppConfig.OutputPath;
            LogMessage("Config loaded");
        }


        private void StartListeners()
        {
            ImageListener = new(ListenForImages);
            ImageListener.Start();
            LogMessage("Listeners started");
        }


        private void ListenForImages()
        {
            DateTime? paintLastConversion = null;
            DateTime? specLastConversion = null;
            while(WindowOpen)
            {
                try
                {
                    if(!string.IsNullOrEmpty(paintFilePath)
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


        #region Helpers


        private void SetOutputPath(string path)
        {
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
                string newPath = pathConfig.IRacingPath;
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
            if(!dialogResult)
            {
                return null;
            }

            string fileName = fileDialog.FileName;
            return File.Exists(fileName)
                ? fileName
                : null; // How do we reach here?
        }


        private void LogMessage(string message)
        {
            this.Dispatcher.Invoke(new Action(() =>
            {
                Paragraph paragraph = new();
                paragraph.Margin = new(0);
                paragraph.Padding = new(0);
                DateTime currentTime = DateTime.Now;
                string timestamp = $"[{currentTime.Hour:00}:{currentTime.Minute:00}:{currentTime.Second:00}.{currentTime.Millisecond:000}] ";
                message = $"{timestamp} {message}";
                paragraph.Inlines.Add(message);
                TextBox_Log.Document.Blocks.Add(paragraph);
            }));
        }


        private string GetFileChecksum(string filePath)
        {
            using var stream = File.OpenRead(filePath);
            byte[] hash = md5.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
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
            string path = Path.GetDirectoryName(this.paintFilePath);
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


        #endregion
    }
}


internal class IRacingPaintPathConfig
{
    public string IRacingPath { get; set; }
}