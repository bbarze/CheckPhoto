using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    /// Interaction logic for DuplicateWindow.xaml
    /// </summary>
    public partial class DuplicateWindow : Window
    {
        static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public static double upLimit;
        public static double lwLimit;

        public DuplicateWindow(List<InspectionDuplicate> duplicateItems, double upperLimit, double lowerLimit)
        {
            InitializeComponent();

            log.Info("Loading items...");

            upLimit = upperLimit;
            lwLimit = lowerLimit;

            dgDuplicate.ItemsSource = duplicateItems;
        }



        private void dgDuplicate_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (dgDuplicate.SelectedCells.Count > 0)
            {
                InspectionDuplicate id = (InspectionDuplicate)dgDuplicate.SelectedCells[0].Item;

                if (!File.Exists(id.FullFileName1))
                {
                    MessageBox.Show($"{id.FullFileName1} do not exist anymore");
                    return;
                }
                if (!File.Exists(id.FullFileName2))
                {
                    MessageBox.Show($"{id.FullFileName2} do not exist anymore");
                    return;
                }

                if (!MainWindow.MineDialogResult(id.FullFileName1, id.FullFileName2, id.Similarity, true, false))
                {
                    return;
                }

                MainWindow.ManageSimilarItems(id.FullFileName1, id.FullFileName2, out bool mfs2t, out bool ds);


            }
        }
    }
}
