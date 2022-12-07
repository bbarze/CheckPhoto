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
using System.Drawing.Imaging;
using FFMpegCore;

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
        private const int DIM_BITMAP = 512;

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
        /// Get the executable's Directory
        /// </summary>
        private static string AssemblyDirectory
        {
            get
            {
                string codeBase = Assembly.GetExecutingAssembly().CodeBase;
                UriBuilder uri = new UriBuilder(codeBase);
                string path = Uri.UnescapeDataString(uri.Path);
                return System.IO.Path.GetDirectoryName(path);
            }
        }

        /// <summary>
        /// Compress the image and convert to black and white. Put the result inside a list
        /// </summary>
        /// <param name="bmpSource"></param>
        /// <returns></returns>
        private static List<bool> GetHash(Bitmap bmpSource)
        {
            List<bool> lResult = new List<bool>();
            //create new image with TxH pixel

            using (Bitmap bmpMin = new Bitmap(bmpSource, new System.Drawing.Size(DIM_BITMAP, DIM_BITMAP)))
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

        /// <summary>
        /// Return the duration of the video passed as parameter
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
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
        /// Calculate the Similarity between two images
        /// </summary>
        /// <param name="img1"></param>
        /// <param name="img2"></param>
        /// <returns></returns>
        private double GetPhotoSimilarity(Bitmap img1, Bitmap img2)
        {
            List<bool> iHashNew = GetHash(img1);
            List<bool> iHashOld = GetHash(img2);
            int maxElement = Math.Max(iHashOld.Count, iHashNew.Count);
            int equalElements = iHashNew.Zip(iHashOld, (i, j) => i == j).Count(eq => eq);
            double similarity = 100 * equalElements / maxElement;
            return similarity;
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
        private CustomTextWriter logWriter;

        /// <summary>
        /// Timer used to print on UI logs
        /// </summary>
        private DispatcherTimer logDispatcherTimer;

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
                logDispatcherTimer.Tick += new EventHandler(dispatcherTimerLog_Tick);
                logDispatcherTimer.Interval = new TimeSpan(0, 0, 5);
                logDispatcherTimer.Start();

                log.Info("...STARTING");

            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
        }

        #region CHECK_EXE

        private bool IsExternalExeOKay()
        {
            if (CheckExternalExe("ffmpeg") && CheckExternalExe("ffprobe") && CheckExternalExe("ffplay"))
            {
                return true;
            }
            return false;
        }

        private bool CheckExternalExe(String appName)
        {
            try
            {
                String exeExtension = ".exe";
                String zipExtension = ".zip";

                String appPath = System.IO.Path.Combine(@AssemblyDirectory, appName + exeExtension);

                if (!File.Exists(appPath))
                {
                    String zipPath = System.IO.Path.Combine(@AssemblyDirectory, appName + zipExtension);
                    if (!File.Exists(zipPath))
                    {
                        String msg = $"Unable to find {appName} executable or zip file";
                        MessageBox.Show(msg);
                        return false;
                    }
                    else
                    {
                        log.Info($"Extacting {zipPath}");
                        System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, @AssemblyDirectory);
                    }
                }
                else
                {
                    log.Debug($"The executable {appName} already exist in {@AssemblyDirectory}");
                }

                return true;
            }
            catch (Exception ex)
            {
                log.Error(ex);
                return false;
            }
        }

        #endregion CHECK_EXE

        #region MANAGE_UI_LOG

        /// <summary>
        /// Manage log to UI
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void dispatcherTimerLog_Tick(object sender, EventArgs e)
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

        #endregion MANAGE_UI_LOG

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
                        log.Info($"{f2Check} will be deleted becouse of it is identcal to {fExisting}");
                        duplicatedIdentical++;
                        FileSystem.DeleteFile(f2Check, UIOption.AllDialogs, RecycleOption.SendToRecycleBin);
                        return;
                    }
                }

                if (IsPhoto(f2Check))
                {
                    foreach (string fExisting in existingFile)
                    {

                        double similarity = GetPhotoSimilarity(new Bitmap(f2Check), new Bitmap(fExisting));
                        log.Info($"{f2Check} is {similarity}% similar to {fExisting}");

                        if (similarity < lowerLimit && skipControlBecouseDifferent)
                        {
                            log.Info($"Will be conisdered as different! lowerLimit={lowerLimit}!");
                            break;
                        }

                        // if are similar more than the limit and skip is true -> do not show the dialog
                        if (similarity < upperLimit || !skipControlBecouseEquals)
                        {
                            if (!MineDialogResult(f2Check, fExisting, similarity, true, false))
                            {
                                break;
                            }
                        }

                        ManageSimilarItems(f2Check, fExisting, out bool imfs2t, out bool ids);

                        if (imfs2t)
                        {
                            movedFromSource2Target++;
                        }
                        else if (ids)
                        {
                            deletedSource++;
                        }

                    }
                }
                else if (IsVideo(f2Check))
                {
                    foreach (string fExisting in existingFile)
                    {

                        TimeSpan duration2Check = GetVideoDuration(f2Check);
                        TimeSpan durationExist = GetVideoDuration(fExisting);

                        bool millisecondDiff = false;

                        if (Math.Round(duration2Check.TotalSeconds) != Math.Round(durationExist.TotalSeconds))
                        {
                            log.Info($"The duration is not matching on seconds, will be conisdered as different!");
                            break;
                        }

                        if (duration2Check != durationExist)
                        {
                            log.Warn($"The duration on millisecond do not match: {duration2Check.ToString(@"hh\:mm\:ss\.fff")} ---  {durationExist.ToString(@"hh\:mm\:ss\.fff")}");
                            millisecondDiff = true;
                        }

                        log.Info($"The duration of both file is " + duration2Check.ToString(@"hh\:mm\:ss"));

                        int n = 5;

                        double tDivided = duration2Check.TotalMilliseconds / n;

                        double tSimilarity = 0;

                        //int j = 0;
                        for (double i = 1; i < duration2Check.TotalMilliseconds; i = i + tDivided)
                        {

                            Bitmap b2Check = FFMpeg.Snapshot(f2Check, new System.Drawing.Size(DIM_BITMAP, DIM_BITMAP), TimeSpan.FromMilliseconds(i));

                            Bitmap bExisting = FFMpeg.Snapshot(fExisting, new System.Drawing.Size(DIM_BITMAP, DIM_BITMAP), TimeSpan.FromMilliseconds(i));

                            double similarity = GetPhotoSimilarity(b2Check, bExisting);

                            tSimilarity = tSimilarity + similarity;

                            log.Info($"{f2Check} is {similarity}% similar to {fExisting} at millisecond {i}");

                            //FFMpeg.Snapshot(f2Check, System.IO.Path.Combine(AssemblyDirectory, "" + j + ".png"), new System.Drawing.Size(DIM_BITMAP, DIM_BITMAP), TimeSpan.FromMilliseconds(i));
                            //j++;
                        }

                        tSimilarity = tSimilarity / n;

                        log.Info($"{f2Check} is {tSimilarity}% similar to {fExisting}");

                        if (!MineDialogResult(f2Check, fExisting, tSimilarity, false, millisecondDiff))
                        {
                            break;
                        }

                        ManageSimilarItems(f2Check, fExisting, out bool vmfs2t, out bool vds);
                        if (vmfs2t)
                        {
                            movedFromSource2Target++;
                        }
                        else if (vds)
                        {
                            deletedSource++;
                        }

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

        /// <summary>
        /// Given two similar items will manage them. 
        /// If the biggest one is the new one: will be replaced the old 
        /// If the biggest one is the old one: will be deleted the new one
        /// </summary>
        /// <param name="f2Check"></param>
        /// <param name="fExisting"></param>
        /// <param name="movedFromSource2Target"></param>
        /// <param name="deletedSource"></param>
        private void ManageSimilarItems(String f2Check, String fExisting, out bool movedFromSource2Target, out bool deletedSource)
        {
            movedFromSource2Target = false;
            deletedSource = false;

            //The images are similar, manage it
            if (GetFileSize(f2Check) > GetFileSize(fExisting))
            {
                //Il nuovo file ha risoluzione maggiore, si sostituisce il vecchio col nuovo
                log.Info($"New file [{f2Check}] is bigger then the old one [{fExisting}]. The old one will be replaced");
                FileSystem.DeleteFile(fExisting, UIOption.AllDialogs, RecycleOption.SendToRecycleBin);
                File.Move(f2Check, fExisting);
                movedFromSource2Target = true;
                return;
            }
            else
            {
                //Il nuovo file ha risoluzione minore, va eliminato
                FileSystem.DeleteFile(f2Check, UIOption.AllDialogs, RecycleOption.SendToRecycleBin);
                log.Info($"New file is smaller than the new one. So {f2Check} will be deleted.");
                deletedSource = true;
                return;
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
        private bool MineDialogResult(String f2Check, String fExisting, double similarity, bool isImg, bool warn)
        {
            try
            {
                return Application.Current.Dispatcher.Invoke(() =>
                {
                    CompareWindow cw = new CompareWindow(f2Check, fExisting, similarity, isImg, warn);
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

                if (!IsExternalExeOKay())
                {
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
