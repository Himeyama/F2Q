using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using System;
using System.Threading.Tasks;
using System.Diagnostics;


namespace F2Q
{
    public sealed partial class Dialog : Page
    {
        public Dialog()
        {
            InitializeComponent();
        }

        public static async Task CreateDialog(MainWindow mainWindow, string title, string contentText)
        {
            ContentDialog dialog = new();
            dialog.XamlRoot = mainWindow.Content.XamlRoot;
            dialog.Title = title;
            dialog.PrimaryButtonText = "OK";
            dialog.DefaultButton = ContentDialogButton.Primary;
            Dialog content = new();
            dialog.Content = content;
            content.DialogMessageTextBlock.Text = contentText;
            await dialog.ShowAsync();
        }

        public static void OpenUrl(string url)
        {
            ProcessStartInfo startInfo = new()
            {
                FileName = url,
                UseShellExecute = true
            };
            Process.Start(startInfo);
        }

        void OnGitHubLinkClick(object _sender, RoutedEventArgs _e){
            OpenUrl("https://github.com/himeyama");
        }
        void OnGitHubRepoLinkClick(object _sender, RoutedEventArgs _e){
            OpenUrl("https://github.com/himeyama/F2Q");
        }
    }
}