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

        public CompareWindow(String newFileName, String oldFileName, double similarity, bool isImg)
        {
            InitializeComponent();

            //https://wpf-tutorial.com/audio-video/how-to-creating-a-complete-audio-video-player/

            try
            {
                tbSimilarity.Text = similarity + "%";

                if (isImg)
                {
                    spVideoCmd.Visibility = Visibility.Collapsed;

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
                    vOld.Source = new Uri(oldFileName);
                    vNew.Source = new Uri(newFileName);

                    _timer.Interval = TimeSpan.FromMilliseconds(60);
                    _timer.Tick += new EventHandler(ticktock);

                    PlayVideo();
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

        /// <summary>
        /// Start the video
        /// </summary>
        void PlayVideo()
        {
            _timer.Start();
            vOld.Play();
            vNew.Play();

            btnStart.Visibility = Visibility.Collapsed;
            btnPause.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Stop the video
        /// </summary>
        void PauseVideo()
        {
            vOld.Stop();
            vNew.Stop();
            _timer.Stop();

            btnPause.Visibility = Visibility.Collapsed;
            btnStart.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Bind slider progress with video
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void ticktock(object sender, EventArgs e)
        {
            timelineSlider.Value = vNew.Position.TotalMilliseconds;
        }

        /// <summary>
        /// Set max value of slider
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void vNew_MediaOpened(object sender, RoutedEventArgs e)
        {
            timelineSlider.Maximum = vNew.NaturalDuration.TimeSpan.TotalMilliseconds;
            lblTot.Text = TimeSpan.FromMilliseconds(timelineSlider.Maximum).ToString(@"hh\:mm\:ss\.fff");
        }

        /// <summary>
        /// On media end stop 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void vNew_MediaEnded(object sender, RoutedEventArgs e)
        {
            PauseVideo();
        }

        /// <summary>
        /// Jump to different parts of the media (seek to).
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SeekToMediaPosition(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            int SliderValue = (int)timelineSlider.Value;

            lblProgressStatus.Text = TimeSpan.FromMilliseconds(timelineSlider.Value).ToString(@"hh\:mm\:ss\.fff");

            // Overloaded constructor takes the arguments days, hours, minutes, seconds, milliseconds.
            // Create a TimeSpan with miliseconds equal to the slider value.
            TimeSpan ts = new TimeSpan(0, 0, 0, 0, SliderValue);
            vNew.Position = ts;
            vOld.Position = ts;
        }

        private void btnPause_Click(object sender, RoutedEventArgs e)
        {
            PauseVideo();
        }

        private void btnStart_Click(object sender, RoutedEventArgs e)
        {
            PlayVideo();
        }

        #endregion VIDEO
    }
}
