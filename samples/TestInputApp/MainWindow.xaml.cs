using GlobalInputHook;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
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

namespace TestInputApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow(IGlobalInput globalInput)
        {
            InitializeComponent();
            DataContext = new MainWindowDataContext(globalInput);
        }
    }

    public class MainWindowDataContext : INotifyPropertyChanged
    {
        private string _text;

        public string Text
        {
            get
            {
                return _text;
            }
            set
            {
                _text = value;
                OnPropertyChanged();
            }
        }


        public MainWindowDataContext(IGlobalInput globalInput)
        {
            globalInput.KeyDown += OnKeyDown;
            globalInput.KeyUp += OnKeyUp;
        }

        private void OnKeyDown(KeyCode keyCode)
        {
            Text = keyCode.ToString() + " Down";
        }

        private void OnKeyUp(KeyCode keyCode)
        {
            Text = keyCode.ToString() + " Up";
        }

        public event PropertyChangedEventHandler PropertyChanged;

        // Create the OnPropertyChanged method to raise the event
        // The calling member's name will be used as the parameter.
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
