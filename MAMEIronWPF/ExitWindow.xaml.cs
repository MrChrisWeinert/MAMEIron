using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;

namespace MAMEIronWPF
{
    /// <summary>
    /// Interaction logic for ExitWindow.xaml
    /// </summary>
    public partial class ExitWindow : Window
    {
        private DateTime _exitWindowStartTime;
        public ExitWindow()
        {
            InitializeComponent();
            _exitWindowStartTime = DateTime.Now;
            List<string> x = new List<string>();
            x.Add("Reboot");
            x.Add("Shutdown");
            ExitListView.ItemsSource = x;
            ExitListView.SelectedItem = ExitListView.Items[0];
        }

        private void ExitListView_KeyDown(object sender, KeyEventArgs e)
        {
            if (DateTime.Now < _exitWindowStartTime.AddSeconds(2))
            {
                //do nothing
            }
            else if (e.Key == Key.V)
            {
                Close();
            }
            else if (e.Key == Key.C)
            {
                if (ExitListView.SelectedItem.ToString() == "Reboot")
                {
                    System.Diagnostics.Process.Start("shutdown.exe", "/r /t 0");
                }
                else if (ExitListView.SelectedItem.ToString() == "Shutdown")
                {
                    System.Diagnostics.Process.Start("shutdown.exe", "/s /t 0");
                }
            }
        }
    }
}