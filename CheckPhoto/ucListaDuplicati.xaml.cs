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

        static IEnumerable<IEnumerable<T>> GetPermutations<T>(IEnumerable<T> items, int count)
        {
            int i = 0;
            foreach (var item in items)
            {
                if (count == 1)
                    yield return new T[] { item };
                else
                {
                    foreach (var result in GetPermutations(items.Skip(i + 1), count - 1))
                        yield return new T[] { item }.Concat(result);
                }

                ++i;
            }
        }


        private void Init(List<String> items)
        {
            lvD.ItemsSource = items;

            IEnumerable<IEnumerable<String>> result = GetPermutations(items, 2);

            String similarityText = String.Empty;

            foreach (IEnumerable<String> c in result)
            {
                MainWindow.AreSimilar(items[0], items[1], DuplicateWindow.upLimit, true, DuplicateWindow.lwLimit, true, true, out double similarity);

                similarityText += $"{similarity} :: <{c.ElementAt(0)}> <{c.ElementAt(1)}>{System.Environment.NewLine}";
            }

            tbSimilarity.Text = similarityText;

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
