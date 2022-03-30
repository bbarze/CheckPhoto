using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Drawing;
using System.IO;
using System.Linq;
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

namespace CheckPhoto
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        static byte[] Sha256HashFile(string file)
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

        static List<bool> GetHash(String bmpSource)
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

        String GetName(String file)
        {
            FileInfo fi = new FileInfo(file);
            return fi.Name;
        }

        long GetSize(String file)
        {
            FileInfo fi = new FileInfo(file);
            return fi.Length;
        }

        static bool isWorking = false;

        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public MainWindow()
        {
            InitializeComponent();

            String pathSource = ConfigurationManager.AppSettings["pathSourceNewPhoto"];
            tbSource.Text = pathSource;

            String pathTarget = ConfigurationManager.AppSettings["pathSourceAlbumPhoto"];
            tbTarget.Text = pathTarget;
        }

        private void CheckFile(String f2Check, String pathTarget, ref int duplicatedIdentical, ref int deletedSource, ref int movedFromSource2Target)
        {
            try
            {
                string f2CheckName = GetName(f2Check);

                int cnt = Directory.GetFiles(pathTarget, f2CheckName, System.IO.SearchOption.AllDirectories).Count();

                if (cnt <= 0)
                {
                    log.Info($"Nel percorso {pathTarget} (e sottocartelle) non ci sono file che si chiamino {f2CheckName}");
                    return;
                }

                log.Info($"----  {f2Check}  ----");

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
                        log.Info($"{f2Check} viene eliminata perchè è identica a {fExisting}");
                        duplicatedIdentical++;
                        FileSystem.DeleteFile(f2Check, UIOption.AllDialogs, RecycleOption.SendToRecycleBin);
                        return;
                    }
                }

                if (IsPhoto(f2Check))
                {
                    foreach (string fExisting in existingFile)
                    {
                        // Check similar file
                        double upperLimit = Convert.ToDouble(ConfigurationManager.AppSettings["upperLimitPercentage"]);
                        bool skipControlBecouseEquals = Convert.ToBoolean(ConfigurationManager.AppSettings["doNotShowUpperAndEqualThanUpperLimit"]);

                        double lowerLimit = Convert.ToDouble(ConfigurationManager.AppSettings["lowerLimitPercentage"]);
                        bool skipControlBecouseDifferent = Convert.ToBoolean(ConfigurationManager.AppSettings["doNotShowLowerThanLowerLimit"]);

                        List<bool> iHashNew = GetHash(f2Check);
                        List<bool> iHashOld = GetHash(fExisting);
                        int maxElement = Math.Max(iHashOld.Count, iHashNew.Count);
                        int equalElements = iHashNew.Zip(iHashOld, (i, j) => i == j).Count(eq => eq);
                        double similarity = 100 * equalElements / maxElement;

                        log.Info($"{f2Check} è simile a {fExisting} al {similarity}%");

                        if (similarity < lowerLimit && skipControlBecouseDifferent)
                        {
                            log.Info($"Sono diverse! Non vengono mostrate, il limite inferiore è {lowerLimit}!");
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
                        if (GetSize(f2Check) > GetSize(fExisting))
                        {
                            //Il nuovo file ha risoluzione maggiore, si sostituisce il vecchio col nuovo
                            FileSystem.DeleteFile(fExisting, UIOption.AllDialogs, RecycleOption.SendToRecycleBin);
                            log.Info($"Il nuovo file {f2Check} ha risoluzione migliore del vecchio file {fExisting}. Lo rimpiazzerà.");
                            File.Move(f2Check, fExisting);
                            movedFromSource2Target++;
                            return;
                        }
                        else
                        {
                            //Il nuovo file ha risoluzione minore, va eliminato
                            FileSystem.DeleteFile(f2Check, UIOption.AllDialogs, RecycleOption.SendToRecycleBin);
                            log.Info($"Il nuovo file aveva risoluzione minore ofìd uguale, viene eliminato: {f2Check}");
                            deletedSource++;
                            return;
                        }
                    }
                }
                else
                {
                    log.Info($"{f2Check} non è una foto!");
                    return;
                }
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
        }


        void RunIt(String pathSource, String pathTarget)
        {
            if (isWorking)
            {
                log.Warn("Is already working!");
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show($"Is already working!");
                });
                return;
            }

            int duplicatedIdentical = 0;
            int deletedSource = 0;
            int movedFromSource2Target = 0;

            try
            {
                isWorking = true;
                log.Info("###########################");
                log.Info("Started!");

                if (!Directory.Exists(pathSource))
                {
                    log.Error($"{pathSource} doesn't exist");
                    return;
                }
                if (!Directory.Exists(pathTarget))
                {
                    log.Error($"{pathTarget} doesn't exist");
                    return;
                }

                String[] fileEntries = Directory.GetFiles(pathSource);

                foreach (string f2Check in fileEntries)
                {
                    CheckFile(f2Check, pathTarget, ref duplicatedIdentical, ref deletedSource, ref movedFromSource2Target);
                }

            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
            isWorking = false;


            log.Info("END: ");
            log.Info("deleted identical: " + duplicatedIdentical);
            log.Info("deleted from folder to check: " + deletedSource);
            log.Info("replaced (moved from folder to check): " + movedFromSource2Target);

            Application.Current.Dispatcher.Invoke(() =>
            {
                MessageBox.Show($"END:{System.Environment.NewLine}" +
                    $"deleted identical: {duplicatedIdentical}{System.Environment.NewLine}" +
                    $"deleted from folder to check: {deletedSource}{System.Environment.NewLine}" +
                    $"replaced (moved from folder to check): {movedFromSource2Target}{System.Environment.NewLine}");
            });

        }

        private bool IsPhoto(string fileName)
        {
            return fileName.ToLower().EndsWith(".png") || fileName.ToLower().EndsWith(".cr2") || fileName.ToLower().EndsWith(".jpg");
        }

        private bool MineDialogResult(String f2Check, String fExisting, double similarity)
        {
            try
            {
                //log.Debug("in here");
                return Application.Current.Dispatcher.Invoke(() =>
                {
                    //log.Debug("Going to show comparingWindows");
                    CompareWindow cw = new CompareWindow(f2Check, fExisting, similarity);
                    cw.ShowDialog();
                    //log.Debug("ComparingWindows showed");
                    if (!cw.DialogResult.HasValue || !cw.DialogResult.Value)
                    {
                        //log.Debug("No result or negative");
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

        void btnCheck_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                String source = tbSource.Text;
                String target = tbTarget.Text;

                Thread processThread = new Thread(delegate ()
                {
                    RunIt(source, target);
                });
                processThread.SetApartmentState(ApartmentState.STA);
                processThread.IsBackground = true;
                processThread.Start();

            }
            catch (Exception ex)
            {
                log.Error(ex);
                Mouse.OverrideCursor = System.Windows.Input.Cursors.Arrow;
            }
        }

        void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            log.Info("Closed!");
        }
    }
}
