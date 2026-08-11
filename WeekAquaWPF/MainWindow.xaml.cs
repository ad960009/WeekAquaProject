using System.ComponentModel;
using System.Windows;
using WeekAquaWPF.ViewModels;

namespace WeekAquaWPF
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            if (DataContext is MainViewModel vm)
            {
                vm.Dispose();
            }
        }
    }
}