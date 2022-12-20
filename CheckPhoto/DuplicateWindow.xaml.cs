using System;
using System.Collections.Generic;
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
        public DuplicateWindow(Dictionary<String, List<String>> duplicateDic)
        {
            InitializeComponent();

            log.Info("Loading items...");

            lv.ItemsSource = duplicateDic;
        }

        private void lv_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }
    }
}
