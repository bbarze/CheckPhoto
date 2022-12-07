using System;
using System.Collections.Generic;
using System.Configuration;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace CheckPhoto
{
    /// <summary>
    /// Interaction logic for CompareWindow.xaml
    /// </summary>
    public partial class CompareWindow : Window
    {
        /// <summary>
        /// Display the file size
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        private String GetSize(String fileName)
        {
            FileInfo fi = new FileInfo(fileName);
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = fi.Length;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }

            // Adjust the format string to your preferences. For example "{0:0.#}{1}" would
            // show a single decimal place, and no space.
            string result = String.Format("{0:0.##} {1}", len, sizes[order]);
            return result;
        }

        static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Timer that keeps aligned Slider Progress with Video Progress
        /// </summary>
        DispatcherTimer _timer = new DispatcherTimer();

        public CompareWindow(String newFileName, String oldFileName, double similarity, bool isImg, bool warn)
        {
            InitializeComponent();

            //https://wpf-tutorial.com/audio-video/how-to-creating-a-complete-audio-video-player/

            try
            {
                tbSimilarity.Text = similarity + "%";

                if (warn)
                {
                    tbSimilarity.Background = new SolidColorBrush(Colors.Red);

                }

                if (isImg)
                {
                    spVideoCmd.Visibility = Visibility.Collapsed;
                    mePlayerN.Visibility = Visibility.Collapsed;
                    mePlayerO.Visibility = Visibility.Collapsed;
                    pbVolumeN.Visibility = Visibility.Collapsed;
                    pbVolumeO.Visibility = Visibility.Collapsed;

                    iNew.Source = GetImage(newFileName);
                    iOld.Source = GetImage(oldFileName);

                    using (var img = System.Drawing.Image.FromFile(newFileName))
                    {
                        int height = img.Height;
                        int width = img.Width;
                        float vRes = img.VerticalResolution;
                        float hRes = img.HorizontalResolution;
                        tbNewInfo.Text = $"{height}x{width} ({vRes}x{hRes})";
                    }
                    using (var img = System.Drawing.Image.FromFile(oldFileName))
                    {
                        int height = img.Height;
                        int width = img.Width;
                        float vRes = img.VerticalResolution;
                        float hRes = img.HorizontalResolution;
                        tbOldInfo.Text = $"{height}x{width} ({vRes}x{hRes})";
                    }
                }
                else
                {

                    iNew.Visibility = Visibility.Collapsed;
                    iOld.Visibility = Visibility.Collapsed;

                    mePlayerN.Source = new Uri(newFileName);
                    mePlayerO.Source = new Uri(oldFileName);

                    btnPause.Visibility = Visibility.Collapsed;

                    _timer.Interval = TimeSpan.FromMilliseconds(60);
                    _timer.Tick += new EventHandler(ticktock);
                    _timer.Start();

                    Play();


                }

                tbNewPath.Text = newFileName;
                tbOldPath.Text = oldFileName;

                tbNewSize.Text = GetSize(newFileName);
                tbOldSize.Text = GetSize(oldFileName);
            }
            catch (Exception ex)
            {
                log.Error($"Loading [{newFileName}] & [{oldFileName}]: {ex}");
            }

        }

        #region UI_RESULT

        void Equals_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            log.Info($"Those are similar");
            this.Close();
        }

        void NotEquals_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            log.Info($"Those are NOT similar");
            this.Close();
        }

        #endregion UI_RESULT

        #region IMAGE

        private BitmapImage GetImage(String fileName)
        {
            using (FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.Read))
            {
                System.Drawing.Image image = System.Drawing.Image.FromStream(fs);

                using (var ms = new MemoryStream())
                {
                    image.Save(ms, System.Drawing.Imaging.ImageFormat.Bmp);
                    ms.Seek(0, SeekOrigin.Begin);

                    var bitmapImage = new BitmapImage();
                    bitmapImage.BeginInit();
                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                    bitmapImage.StreamSource = ms;
                    bitmapImage.EndInit();

                    return bitmapImage;
                }

            }
        }

        #endregion IMAGE

        #region VIDEO

        void ticktock(object sender, EventArgs e)
        {
            try
            {
                if ((mePlayerN.Source != null) && (mePlayerN.NaturalDuration.HasTimeSpan) && (!userIsDraggingSlider))
                {
                    sliProgress.Minimum = 0;
                    sliProgress.Maximum = mePlayerN.NaturalDuration.TimeSpan.TotalSeconds;
                    sliProgress.Value = mePlayerN.Position.TotalSeconds;
                    lblTot.Text = mePlayerN.NaturalDuration.TimeSpan.ToString(@"hh\:mm\:ss");

                    tbNewInfo.Text = $"{mePlayerN.NaturalVideoHeight}x{mePlayerN.NaturalVideoWidth} ({mePlayerN.NaturalDuration.TimeSpan.ToString(@"hh\:mm\:ss\.fff")})";
                    tbOldInfo.Text = $"{mePlayerO.NaturalVideoHeight}x{mePlayerO.NaturalVideoWidth} ({mePlayerO.NaturalDuration.TimeSpan.ToString(@"hh\:mm\:ss\.fff")})";

                }
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
        }

        private void btnPause_Click(object sender, RoutedEventArgs e)
        {
            mePlayerN.Pause();
            mePlayerO.Pause();
            btnPause.Visibility = Visibility.Collapsed;
            btnStart.Visibility = Visibility.Visible;
        }

        private void btnStart_Click(object sender, RoutedEventArgs e)
        {
            Play();
        }

        private void Play()
        {
            mePlayerN.Play();
            mePlayerO.Play();
            btnPause.Visibility = Visibility.Visible;
            btnStart.Visibility = Visibility.Collapsed;
        }

        private bool userIsDraggingSlider = false;

        private void sliProgress_DragStarted(object sender, DragStartedEventArgs e)
        {
            userIsDraggingSlider = true;
        }

        private void sliProgress_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            userIsDraggingSlider = false;
            mePlayerN.Position = TimeSpan.FromSeconds(sliProgress.Value);
            mePlayerO.Position = TimeSpan.FromSeconds(sliProgress.Value);
        }

        private void sliProgress_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            lblProgressStatus.Text = TimeSpan.FromSeconds(sliProgress.Value).ToString(@"hh\:mm\:ss");
        }

        private void pbVolumeN_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            mePlayerN.Volume += (e.Delta > 0) ? 0.1 : -0.1;
        }

        private void pbVolumeO_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            mePlayerO.Volume += (e.Delta > 0) ? 0.1 : -0.1;
        }

        #endregion VIDEO
    }
}
