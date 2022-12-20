using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace CheckPhoto
{
    /// <summary>
    /// Interaction logic for ucListaDuplicati.xaml
    /// </summary>
    public partial class ucListaDuplicati : UserControl
    {

        static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public static readonly DependencyProperty DuplicatiProprety = DependencyProperty.Register("Duplicati", typeof(List<String>), typeof(ucListaDuplicati), new PropertyMetadata(new List<String>()));
        public List<String> Duplicati
        {
            get { return (List<String>)GetValue(DuplicatiProprety); }
            set { SetValue(DuplicatiProprety, value); }
        }

        public ucListaDuplicati()
        {

            InitializeComponent();

            Loaded += (sender, args) =>
            {
                Init(Duplicati);
            };
        }

        private void Init(List<String> items)
        {
            lvD.ItemsSource = items;
        }

        private void TextBlock_MouseDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (e.ClickCount == 2)
                {
                    String cmd = (((System.Windows.FrameworkElement)sender).DataContext).ToString();

                    log.Info($@"Opening <{cmd}>");

                    Process.Start(@cmd);
                }
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
        }
    }
}
