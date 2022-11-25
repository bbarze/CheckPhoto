using Microsoft.VisualBasic.FileIO;
using Microsoft.WindowsAPICodePack.Shell.PropertySystem;
using Microsoft.WindowsAPICodePack.Shell;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Tab;

namespace CheckPhoto
{

    /// <summary>
    /// Redirect Console to String List
    /// </summary>
    public class CustomTextWriter : TextWriter
    {
        public List<String> sList = new List<String>();

        public override void Write(String value)
        {
            sList.Add(value);
        }

        public override Encoding Encoding
        {
            get { return Encoding.Default; }
        }
    }


    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// Calculate the SHA256 of the file passed
        /// </summary>
        /// <param name="file">File to calculate SHA256</param>
        /// <returns>SHA256 of the file</returns>
        private static byte[] Sha256HashFile(string file)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                using (Stream input = File.OpenRead(file))
                {

                    byte[] res = sha256.ComputeHash(input);

                    input.Close();

                    return res;
                }
            }
        }

        /// <summary>
        /// Compress the image and convert to black and white. Put the result inside a list
        /// </summary>
        /// <param name="bmpSource"></param>
        /// <returns></returns>
        private static List<bool> GetHash(String bmpSource)
        {
            List<bool> lResult = new List<bool>();
            //create new image with 16x16 pixel
            using (Bitmap bmpMax = new Bitmap(bmpSource))
            {
                using (Bitmap bmpMin = new Bitmap(bmpMax, new System.Drawing.Size(256, 256)))
                {
                    for (int j = 0; j < bmpMin.Height; j++)
                    {
                        for (int i = 0; i < bmpMin.Width; i++)
                        {
                            //reduce colors to true / false                
                            lResult.Add(bmpMin.GetPixel(i, j).GetBrightness() < 0.5f);
                        }
                    }
                    return lResult;
                }
            }
        }

        /// <summary>
        /// Based on filename extension, chek if the file passed is a photo
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        private static bool IsPhoto(String fileName)
        {
            return fileName.ToLower().EndsWith(".png") || fileName.ToLower().EndsWith(".cr2") || fileName.ToLower().EndsWith(".jpg");
        }

        /// <summary>
        /// Based on filename extension, chek if the file passed is a video
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        private static bool IsVideo(String fileName)
        {
            return fileName.ToLower().EndsWith(".mp4") || fileName.ToLower().EndsWith(".avi");
        }

        private static String GetFileName(String fileName)
        {
            FileInfo fi = new FileInfo(fileName);
            return fi.Name;
        }

        private static long GetFileSize(String fileName)
        {
            FileInfo fi = new FileInfo(fileName);
            return fi.Length;
        }

        /// <summary>
        /// Indicate if the process is running or not
        /// </summary>
        private static bool isWorking = false;

        /// <summary>
        /// Manage Log4Net
        /// </summary>
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Used to redirect through log4net console log to String List
        /// </summary>
        CustomTextWriter logWriter;

        /// <summary>
        /// Timer used to print on UI logs
        /// </summary>
        DispatcherTimer logDispatcherTimer;

        public MainWindow()
        {
            InitializeComponent();

            try
            {
                String pathSource = ConfigurationManager.AppSettings["pathSourceNewPhoto"];
                tbSource.Text = pathSource;

                String pathTarget = ConfigurationManager.AppSettings["pathSourceAlbumPhoto"];
                tbTarget.Text = pathTarget;

                bool compareWindowUL = bool.Parse(ConfigurationManager.AppSettings["doNotShowUpperAndEqualThanUpperLimit"]);
                String uL = ConfigurationManager.AppSettings["upperLimitPercentage"];
                cbULimit.IsChecked = compareWindowUL;
                tbULimit.Text = uL;

                bool compareWindowLL = bool.Parse(ConfigurationManager.AppSettings["doNotShowLowerThanLowerLimit"]);
                String lL = ConfigurationManager.AppSettings["lowerLimitPercentage"];
                cbLLimit.IsChecked = compareWindowLL;
                tbLLimit.Text = lL;

                logWriter = new CustomTextWriter();
                Console.SetOut(logWriter);

                logDispatcherTimer = new System.Windows.Threading.DispatcherTimer();
                logDispatcherTimer.Tick += new EventHandler(dispatcherTimer_Tick);
                logDispatcherTimer.Interval = new TimeSpan(0, 0, 5);
                logDispatcherTimer.Start();

                log.Info("...STARTING");

            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
        }

        /// <summary>
        /// Manage log to UI
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void dispatcherTimer_Tick(object sender, EventArgs e)
        {
            List<String> newLogs = logWriter.sList.ToList();

            List<String> showedLogs = lvLog.Items.SourceCollection.Cast<String>().ToList();

            logWriter.sList.Clear();

            if (showedLogs.Count > 30 && newLogs.Count > 0)
            {
                lvLog.ItemsSource = newLogs;
            }
            else
            {
                showedLogs.AddRange(newLogs);
                lvLog.ItemsSource = showedLogs;
            }

            var selectedIndex = lvLog.Items.Count - 1;
            if (selectedIndex < 0)
            {
                return;
            }

            lvLog.SelectedIndex = selectedIndex;
            lvLog.UpdateLayout();

            lvLog.ScrollIntoView(lvLog.SelectedItem);

        }

        /// <summary>
        /// If the file that is passed already exist in target folder, it will be deleted. 
        /// If exist a similar one will be asked to user if the two file are the same.
        /// </summary>
        /// <param name="f2Check">File that will be checked</param>
        /// <param name="pathTarget">Target path</param>
        /// <param name="upperLimit"></param>
        /// <param name="skipControlBecouseEquals"></param>
        /// <param name="lowerLimit"></param>
        /// <param name="skipControlBecouseDifferent"></param>
        /// <param name="duplicatedIdentical">Counter for statistical porpouse. Identical file found and deleted from source folder</param>
        /// <param name="deletedSource">Counter for statistical porpouse. File from source is similar to file in target but has less resolution so deleted</param>
        /// <param name="movedFromSource2Target">Counter for statistical porpouse. File from source is similar to file in target but has grater resolution so replace the file in target</param>
        private void CheckFile(String f2Check, String pathTarget,
            double upperLimit, bool skipControlBecouseEquals,
            double lowerLimit, bool skipControlBecouseDifferent,
            ref long duplicatedIdentical, ref long deletedSource, ref long movedFromSource2Target)
        {
            try
            {
                string f2CheckName = GetFileName(f2Check);

                int cnt = Directory.GetFiles(pathTarget, f2CheckName, System.IO.SearchOption.AllDirectories).Count();

                if (cnt <= 0)
                {
                    log.Info($"Inside {pathTarget} there are no file named {f2CheckName}");
                    return;
                }

                String[] existingFile = Directory.GetFiles(pathTarget, f2CheckName, System.IO.SearchOption.AllDirectories);

                //La prima iterazione viene utilizzata per controllare se c'è un file identico!
                foreach (string fExisting in existingFile)
                {
                    // Check identical file
                    byte[] hash1 = Sha256HashFile(fExisting);
                    byte[] hash2 = Sha256HashFile(f2Check);
                    bool same = hash1.SequenceEqual(hash2);

                    if (same)
                    {
                        log.Info($"{f2Check} will be deleted becouse of is identcal to {fExisting}");
                        duplicatedIdentical++;
                        FileSystem.DeleteFile(f2Check, UIOption.AllDialogs, RecycleOption.SendToRecycleBin);
                        return;
                    }
                }

                if (IsPhoto(f2Check))
                {
                    foreach (string fExisting in existingFile)
                    {

                        List<bool> iHashNew = GetHash(f2Check);
                        List<bool> iHashOld = GetHash(fExisting);
                        int maxElement = Math.Max(iHashOld.Count, iHashNew.Count);
                        int equalElements = iHashNew.Zip(iHashOld, (i, j) => i == j).Count(eq => eq);
                        double similarity = 100 * equalElements / maxElement;

                        log.Info($"{f2Check} is {similarity}% similar to {fExisting}");

                        if (similarity < lowerLimit && skipControlBecouseDifferent)
                        {
                            log.Info($"Will be conisdered as different! lowerLimit={lowerLimit}!");
                            break;
                        }

                        // if are similar more than the limit and skip is true -> do not show the dialog
                        if (similarity < upperLimit || !skipControlBecouseEquals)
                        {
                            if (!MineDialogResult(f2Check, fExisting, similarity))
                            {
                                break;
                            }
                        }

                        //The images are similar, manage it
                        if (GetFileSize(f2Check) > GetFileSize(fExisting))
                        {
                            //Il nuovo file ha risoluzione maggiore, si sostituisce il vecchio col nuovo
                            log.Info($"New file [{f2Check}] has a better resoluiton then the old one [{fExisting}]. The old one will be replaced");
                            FileSystem.DeleteFile(fExisting, UIOption.AllDialogs, RecycleOption.SendToRecycleBin);
                            File.Move(f2Check, fExisting);
                            movedFromSource2Target++;
                            return;
                        }
                        else
                        {
                            //Il nuovo file ha risoluzione minore, va eliminato
                            FileSystem.DeleteFile(f2Check, UIOption.AllDialogs, RecycleOption.SendToRecycleBin);
                            log.Info($"New file has a lower or equal resolution than the new one. So {f2Check} will be deleted.");
                            deletedSource++;
                            return;
                        }
                    }
                }
                else if (IsVideo(f2Check))
                {
                    foreach (string fExisting in existingFile)
                    {
                        TimeSpan duration2Check = GetVideoDuration(f2Check);
                        TimeSpan durationExist = GetVideoDuration(fExisting);

                        if (duration2Check != durationExist)
                        {
                            log.Info($"The duration is different will be conisdered as different!");
                            break;
                        }

                        log.Info($"The duration of both file is {duration2Check.ToString("HH:ss:mmm")}");



                    }
                }
                else
                {
                    log.Info($"{f2Check} is not a photo and is not a video!");
                    return;
                }
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
        }

        private static TimeSpan GetVideoDuration(string filePath)
        {
            using (var shell = ShellObject.FromParsingName(filePath))
            {
                IShellProperty prop = shell.Properties.System.Media.Duration;
                var t = (ulong)prop.ValueAsObject;
                return TimeSpan.FromTicks((long)t);
            }
        }

        /// <summary>
        /// Start the comparison of the files present in source folder.
        /// The comparison is made by filename
        /// </summary>
        /// <param name="pathSource">Source path</param>
        /// <param name="pathTarget">Target path</param>
        /// <param name="upperLimit"></param>
        /// <param name="skipControlBecouseEquals"></param>
        /// <param name="lowerLimit"></param>
        /// <param name="skipControlBecouseDifferent"></param>
        void RunIt(String pathSource, String pathTarget,
            double upperLimit, bool skipControlBecouseEquals,
            double lowerLimit, bool skipControlBecouseDifferent)
        {

            long duplicatedIdentical = 0;
            long deletedSource = 0;
            long movedFromSource2Target = 0;
            long totItems = 0;
            long iItems = 0;

            try
            {
                log.Info("######################################################");
                log.Info("Started!");

                if (!Directory.Exists(pathSource))
                {
                    log.Error($"PATH: {pathSource} doesn't exist");
                    JobFinished();
                    return;
                }
                if (!Directory.Exists(pathTarget))
                {
                    log.Error($"PATH: {pathTarget} doesn't exist");
                    JobFinished();
                    return;
                }

                String[] fileEntries = Directory.GetFiles(pathSource, "*", System.IO.SearchOption.AllDirectories);

                totItems = fileEntries.Count();

                foreach (string f2Check in fileEntries)
                {
                    iItems++;

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        pb.Value = (totItems / 100) * iItems;
                    });

                    log.Info($" ---------------------------------- [{iItems}/{totItems}] {pathTarget}  ---------------------------------- ");

                    CheckFile(f2Check, pathTarget, upperLimit, skipControlBecouseEquals, lowerLimit, skipControlBecouseDifferent,
                        ref duplicatedIdentical, ref deletedSource, ref movedFromSource2Target);
                }

            }
            catch (Exception ex)
            {
                log.Error(ex);
            }

            log.Info("END: ");
            log.Info("deleted identical: " + duplicatedIdentical);
            log.Info("deleted from folder to check: " + deletedSource);
            log.Info("replaced (moved from folder to check): " + movedFromSource2Target);

            JobFinished();

        }

        private void JobFinished()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                isWorking = false;
                btnCheck.IsEnabled = true;
                pb.Value = 0;
                MessageBox.Show($"END");
            });
        }

        /// <summary>
        /// Showned an instance of CompareWindow, so the user can said if two images are the same thing or not 
        /// </summary>
        /// <param name="f2Check">File present inside source folder</param>
        /// <param name="fExisting">File present inside target folder</param>
        /// <param name="similarity">Similarity calculated between the two file</param>
        /// <returns>True if the two file are the same else false</returns>
        private bool MineDialogResult(String f2Check, String fExisting, double similarity)
        {
            try
            {
                return Application.Current.Dispatcher.Invoke(() =>
                {
                    CompareWindow cw = new CompareWindow(f2Check, fExisting, similarity);
                    cw.ShowDialog();
                    if (!cw.DialogResult.HasValue || !cw.DialogResult.Value)
                    {
                        return false;
                    }
                    return true;
                });
            }
            catch (Exception e)
            {
                log.Error(e);
                return false;
            }
        }

        /// <summary>
        /// Clicked the button to start the comparison inside a thread
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void btnCheck_Click(object sender, RoutedEventArgs e)
        {
            try
            {

                if (isWorking)
                {
                    log.Warn("Is already working!");
                    MessageBox.Show($"Is already working!");
                    return;
                }

                isWorking = true;
                btnCheck.IsEnabled = false;

                String source = tbSource.Text;
                String target = tbTarget.Text;

                double upperLimit = Convert.ToDouble(tbULimit.Text);
                bool skipControlBecouseEquals = cbULimit.IsChecked.Value;
                double lowerLimit = Convert.ToDouble(tbLLimit.Text);
                bool skipControlBecouseDifferent = cbLLimit.IsChecked.Value;

                Thread processThread = new Thread(delegate ()
                {
                    RunIt(source, target, upperLimit, skipControlBecouseEquals, lowerLimit, skipControlBecouseDifferent);
                });
                processThread.SetApartmentState(ApartmentState.STA);
                processThread.IsBackground = true;
                processThread.Start();

            }
            catch (Exception ex)
            {
                log.Error(ex);
                JobFinished();
            }
        }

        /// <summary>
        /// Insert or update the app.config file using the value and the key passed
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public static void AddOrUpdateAppSettings(string key, string value)
        {
            try
            {
                var configFile = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                var settings = configFile.AppSettings.Settings;
                if (settings[key] == null)
                {
                    settings.Add(key, value);
                }
                else
                {
                    settings[key].Value = value;
                }
                configFile.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection(configFile.AppSettings.SectionInformation.Name);
            }
            catch (ConfigurationErrorsException)
            {
                log.Error($"Error writing app settings: <{key}> - <{value}>");
            }
        }

        /// <summary>
        /// Application closing
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            log.Info("Closed!");
            Environment.Exit(Environment.ExitCode);
        }

        private void btnS_Click(object sender, RoutedEventArgs e)
        {
            String p = GetFolderPath();
            if (p.Length > 0)
            {
                tbSource.Text = p;
            }
        }

        private static String GetFolderPath()
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                System.Windows.Forms.DialogResult result = dialog.ShowDialog();
                if (result == System.Windows.Forms.DialogResult.OK)
                {
                    return dialog.SelectedPath;
                }
            }
            return String.Empty;
        }

        private void btnT_Click(object sender, RoutedEventArgs e)
        {
            String p = GetFolderPath();
            if (p.Length > 0)
            {
                tbTarget.Text = p;
            }
        }

        private void btnOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start("explorer.exe", @AssemblyDirectory);
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }

        }

        private void btnFindDuplicate_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("TODO!");
            //TODO cerca i file duplicati
        }

        public static string AssemblyDirectory
        {
            get
            {
                string codeBase = Assembly.GetExecutingAssembly().CodeBase;
                UriBuilder uri = new UriBuilder(codeBase);
                string path = Uri.UnescapeDataString(uri.Path);
                return System.IO.Path.GetDirectoryName(path);
            }
        }

        private void btnSaveSetting_Click(object sender, RoutedEventArgs e)
        {
            AddOrUpdateAppSettings("pathSourceNewPhoto", tbSource.Text);
            AddOrUpdateAppSettings("pathSourceAlbumPhoto", tbTarget.Text);
            AddOrUpdateAppSettings("doNotShowUpperAndEqualThanUpperLimit", cbULimit.IsChecked.Value.ToString());
            AddOrUpdateAppSettings("upperLimitPercentage", tbULimit.Text);
            AddOrUpdateAppSettings("doNotShowLowerThanLowerLimit", cbULimit.IsChecked.Value.ToString());
            AddOrUpdateAppSettings("lowerLimitPercentage", tbLLimit.Text);
        }
    }
}
