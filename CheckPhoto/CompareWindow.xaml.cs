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

namespace CheckPhoto
{
    /// <summary>
    /// Interaction logic for CompareWindow.xaml
    /// </summary>
    public partial class CompareWindow : Window
    {

        String GetSize(String fileName)
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

        BitmapImage GetImage(String fileName)
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

        static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public CompareWindow(String newFileName, String oldFileName, double similarity)
        {
            InitializeComponent();

            try
            {
                tbSimilarity.Text = similarity + "%";

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
    }
}
