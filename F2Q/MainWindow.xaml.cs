﻿using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT;
using ZXing;
using ZXing.QrCode;

namespace F2Q
{
    public sealed partial class MainWindow : Window
    {
        WindowsSystemDispatcherQueueHelper m_wsdqHelper;
        MicaController m_backdropController;
        SystemBackdropConfiguration m_configurationSource;

        string plainText = "";
        string base64text;

        void LogMessage(string message)
        {
            string logFilePath = "log.txt";
            try
            {
                using (StreamWriter writer = new StreamWriter(logFilePath, true))
                {
                    writer.WriteLine($"[{DateTime.Now}] {message}");
                }
            }
            catch (Exception ex)
            {
                // エラー処理
                Console.WriteLine($"Logging failed: {ex.Message}");
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            Title = "F2Q";
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(DragArea);
            TrySetSystemBackdrop();
        }

        bool TrySetSystemBackdrop()
        {
            if (MicaController.IsSupported())
            {
                m_wsdqHelper = new WindowsSystemDispatcherQueueHelper();
                m_wsdqHelper.EnsureWindowsSystemDispatcherQueueController();
                m_configurationSource = new SystemBackdropConfiguration();
                Activated += Window_Activated;
                Closed += Window_Closed;
                ((FrameworkElement)Content).ActualThemeChanged += Window_ThemeChanged;
                m_configurationSource.IsInputActive = true;
                SetConfigurationSourceTheme();

                m_backdropController = new MicaController();
                m_backdropController.AddSystemBackdropTarget(this.As<Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop>());
                m_backdropController.SetSystemBackdropConfiguration(m_configurationSource);
                return true;
            }
            return false;
        }

        void Window_Activated(object _sender, WindowActivatedEventArgs args)
        {
            m_configurationSource.IsInputActive = args.WindowActivationState != WindowActivationState.Deactivated;
        }

        void Window_Closed(object _sender, WindowEventArgs _args)
        {
            if (m_backdropController != null)
            {
                m_backdropController.Dispose();
                m_backdropController = null;
            }
            Activated -= Window_Activated;
            m_configurationSource = null;
        }

        void Window_ThemeChanged(FrameworkElement _sender, object _args)
        {
            if (m_configurationSource != null)
            {
                SetConfigurationSourceTheme();
            }
        }

        void SetConfigurationSourceTheme()
        {
            switch (((FrameworkElement)Content).ActualTheme)
            {
                case ElementTheme.Dark:
                    m_configurationSource.Theme = Microsoft.UI.Composition.SystemBackdrops.SystemBackdropTheme.Dark;
                    break;
                case ElementTheme.Light:
                    m_configurationSource.Theme = Microsoft.UI.Composition.SystemBackdrops.SystemBackdropTheme.Light;
                    break;
                case ElementTheme.Default:
                    m_configurationSource.Theme = Microsoft.UI.Composition.SystemBackdrops.SystemBackdropTheme.Default;
                    break;
            }
        }

        void Exit(object _sender, RoutedEventArgs _e)
        {
            Close();
        }

        async void Open(object _sender, RoutedEventArgs _e)
        {
            FileOpenPicker openPicker = new();
            IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WinRT.Interop.InitializeWithWindow.Initialize(openPicker, hWnd);
            openPicker.ViewMode = PickerViewMode.Thumbnail;
            openPicker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            openPicker.FileTypeFilter.Add(".txt");
            openPicker.FileTypeFilter.Add(".md");
            StorageFile file = await openPicker.PickSingleFileAsync();
            if (file == null) return;

            try
            {
                Windows.Storage.Streams.IBuffer buffer = await FileIO.ReadBufferAsync(file);
                using (var dataReader = Windows.Storage.Streams.DataReader.FromBuffer(buffer))
                {
                    byte[] fileData = new byte[buffer.Length];
                    dataReader.ReadBytes(fileData);
                    base64text = Convert.ToBase64String(fileData);
                    plainText = System.Text.Encoding.UTF8.GetString(fileData);
                }
            }
            catch (Exception ex)
            {
                // エラー処理
                LogMessage($"File read failed: {ex.Message}");
            }

            Refresh();
        }

        void RadioRefresh(object _sender, RoutedEventArgs _e)
        {
            Refresh();
        }

        void About(object _sender, RoutedEventArgs _e)
        {
            _ = Dialog.CreateDialog(this, "F2Q", "© 2023-2024 ひかり");
        }

        void Refresh()
        {
            if (string.IsNullOrEmpty(plainText)) return;

            Paragraph paragraph = new();
            Run run = new() { Text = plainText };
            paragraph.Inlines.Add(run);
            DataText.Blocks.Clear();
            DataText.Blocks.Add(paragraph);
            string text = SetDataTextUTF8Base64.IsChecked ? $"data:text/plain;charset=UTF-8;base64,{base64text}" : plainText;

            try
            {
                BarcodeWriter writer = new()
                {
                    Format = BarcodeFormat.QR_CODE,
                    Options = new QrCodeEncodingOptions
                    {
                        Height = 2000,
                        Width = 2000,
                        CharacterSet = "UTF-8",
                    }
                };
                Bitmap bitmap = writer.Write(text);
                BitmapImage bitmapImage = new();
                using (MemoryStream stream = new())
                {
                    bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                    stream.Position = 0;
                    bitmapImage.SetSource(stream.AsRandomAccessStream());
                }
                QRCodeImage.Source = bitmapImage;
            }
            catch (Exception ex)
            {
                LogMessage($"QR code generation failed: {ex.Message}");
            }
        }
    }

    class WindowsSystemDispatcherQueueHelper
    {
        [StructLayout(LayoutKind.Sequential)]
        struct DispatcherQueueOptions
        {
            internal int dwSize;
            internal int threadType;
            internal int apartmentType;
        }

        [DllImport("CoreMessaging.dll")]
        static extern int CreateDispatcherQueueController([In] DispatcherQueueOptions options, [In, Out, MarshalAs(UnmanagedType.IUnknown)] ref object dispatcherQueueController);

        object m_dispatcherQueueController = null;
        public void EnsureWindowsSystemDispatcherQueueController()
        {
            if (Windows.System.DispatcherQueue.GetForCurrentThread() != null)
            {
                return;
            }

            if (m_dispatcherQueueController == null)
            {
                DispatcherQueueOptions options;
                options.dwSize = Marshal.SizeOf(typeof(DispatcherQueueOptions));
                options.threadType = 2;    // DQTYPE_THREAD_CURRENT
                options.apartmentType = 2; // DQTAT_COM_STA

                CreateDispatcherQueueController(options, ref m_dispatcherQueueController);
            }
        }
    }
}