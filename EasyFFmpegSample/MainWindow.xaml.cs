using EasyFFmpeg;
using System;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace EasyFFmpegSample
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : Window
    {
        Dispatcher dispatcher = Application.Current.Dispatcher;

        EasyFFmpegManager easyFFmpeg = new EasyFFmpegManager();

        public MainWindow()
        {
            InitializeComponent();
            //rtsp://wowzaec2demo.streamlock.net/vod/mp4:BigBuckBunny_115k.mp4
        }

        private void Play_Button_Click(object sender, RoutedEventArgs e)
        {
            string url = URL_TextBox.Text;
            int type = VType_ComboBox.SelectedIndex;

            easyFFmpeg.PlayVideo(url, (VideoInputType)type);
            easyFFmpeg.VideoFrameReceived += VideoFrameReceived;
        }

        private void VideoFrameReceived(BitmapImage frame)
        {
            dispatcher.BeginInvoke((Action)(() =>
            {
                image.Source = frame;
            }));
        }

        private void Record_Button_Checked(object sender, RoutedEventArgs e)
        {
            string fileName = DateTime.Now.ToString("yyMMdd_hh.mm.ss") + ".mp4";
            easyFFmpeg.RecordVideo(fileName);
        }

        private void Record_Button_Unchecked(object sender, RoutedEventArgs e)
        {
            easyFFmpeg.StopRecord();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            easyFFmpeg.DisposeFFmpeg();
        }

        private void Stop_Button_Click(object sender, RoutedEventArgs e)
        {
            Record_Button.IsChecked = false;
            easyFFmpeg.DisposeFFmpeg();
        }
    }
}
