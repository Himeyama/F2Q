// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using Microsoft.UI.Xaml;
using static System.Net.Mime.MediaTypeNames;
using ZXing.QrCode;
using ZXing;
using System.Drawing;
using Microsoft.UI.Xaml.Media;
using System.Runtime.InteropServices;
using System;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System.IO;
using Windows.Storage.Pickers;
using Windows.Storage;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI;
using Windows.UI.ViewManagement;
using Windows.ApplicationModel.Core;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using System.Runtime.InteropServices; // For DllImport
using WinRT;
using Windows.ApplicationModel;
// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace File2QR
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        WindowsSystemDispatcherQueueHelper m_wsdqHelper; // See below for implementation.
        MicaController m_backdropController;
        SystemBackdropConfiguration m_configurationSource;

        public MainWindow()
        {
            this.InitializeComponent();
            //Window window = App.MainWindow;
            ExtendsContentIntoTitleBar = true;  // enable custom titlebar
            SetTitleBar(AppTitleBar);
            AppTitle.Text = AppInfo.Current.DisplayInfo.DisplayName;
            TrySetSystemBackdrop();
        }

        bool TrySetSystemBackdrop()
        {
            if (Microsoft.UI.Composition.SystemBackdrops.MicaController.IsSupported())
            {
                m_wsdqHelper = new WindowsSystemDispatcherQueueHelper();
                m_wsdqHelper.EnsureWindowsSystemDispatcherQueueController();

                // Create the policy object.
                m_configurationSource = new SystemBackdropConfiguration();
                this.Activated += Window_Activated;
                this.Closed += Window_Closed;
                ((FrameworkElement)this.Content).ActualThemeChanged += Window_ThemeChanged;

                // Initial configuration state.
                m_configurationSource.IsInputActive = true;
                SetConfigurationSourceTheme();

                m_backdropController = new Microsoft.UI.Composition.SystemBackdrops.MicaController();

            // Enable the system backdrop.
            // Note: Be sure to have "using WinRT;" to support the Window.As<...>() call.
            m_backdropController.AddSystemBackdropTarget(this.As<Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop>());
                m_backdropController.SetSystemBackdropConfiguration(m_configurationSource);
                return true; // succeeded
            }

            return false; // Mica is not supported on this system
        }

        private void Window_Activated(object sender, WindowActivatedEventArgs args)
        {
            m_configurationSource.IsInputActive = args.WindowActivationState != WindowActivationState.Deactivated;
        }

        private void Window_Closed(object sender, WindowEventArgs args)
        {
            // Make sure any Mica/Acrylic controller is disposed
            // so it doesn't try to use this closed window.
            if (m_backdropController != null)
            {
                m_backdropController.Dispose();
                m_backdropController = null;
            }
            this.Activated -= Window_Activated;
            m_configurationSource = null;
        }

        private void Window_ThemeChanged(FrameworkElement sender, object args)
        {
            if (m_configurationSource != null)
            {
                SetConfigurationSourceTheme();
            }
        }

        private void SetConfigurationSourceTheme()
        {
            switch (((FrameworkElement)this.Content).ActualTheme)
            {
                case ElementTheme.Dark: m_configurationSource.Theme = Microsoft.UI.Composition.SystemBackdrops.SystemBackdropTheme.Dark; break;
                case ElementTheme.Light: m_configurationSource.Theme = Microsoft.UI.Composition.SystemBackdrops.SystemBackdropTheme.Light; break;
                case ElementTheme.Default: m_configurationSource.Theme = Microsoft.UI.Composition.SystemBackdrops.SystemBackdropTheme.Default; break;
            }
        }

        private void Exit(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private async void Open(object sender, RoutedEventArgs e)
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
            byte[] fileData = File.ReadAllBytes(file.Path);
            string base64text = Convert.ToBase64String(fileData);

            Paragraph paragraph = new();
            Run run = new();
            run.Text = System.Text.Encoding.UTF8.GetString(fileData);
            paragraph.Inlines.Add(run);
            DataText.Blocks.Clear();
            DataText.Blocks.Add(paragraph);

            string text = $"data:text/plain;charset=UTF-8;base64,{base64text}";
            BarcodeWriter writer = new BarcodeWriter
            {
                Format = BarcodeFormat.QR_CODE,
                Options = new QrCodeEncodingOptions
                {
                    Height = 2000,
                    Width = 2000
                }
            };

            Bitmap bitmap = writer.Write(text);
            BitmapImage bitmapImage = new BitmapImage();
            using (MemoryStream stream = new MemoryStream())
            {
                bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                stream.Position = 0;
                bitmapImage.SetSource(stream.AsRandomAccessStream());
            }
            QRCodeImage.Source = bitmapImage;
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
        private static extern int CreateDispatcherQueueController([In] DispatcherQueueOptions options, [In, Out, MarshalAs(UnmanagedType.IUnknown)] ref object dispatcherQueueController);

        object m_dispatcherQueueController = null;
        public void EnsureWindowsSystemDispatcherQueueController()
        {
            if (Windows.System.DispatcherQueue.GetForCurrentThread() != null)
            {
                // one already exists, so we'll just use it.
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
