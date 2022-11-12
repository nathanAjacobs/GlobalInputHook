using GlobalInputHook;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace TestInputApp.Core
{
    public interface IGlobalInput
    {
        event Action<KeyCode> KeyDown;
        event Action<KeyCode> KeyUp;
    }

    public class MyGlobalInputHookService : GlobalInputHookService, IGlobalInput
    {
        public event Action<KeyCode> KeyDown;
        public event Action<KeyCode> KeyUp;

        protected override void OnError(Exception exception)
        {
            throw exception;
        }

        protected override void OnKeyDown(KeyCode keyCode)
        {
            KeyDown?.Invoke(keyCode);
        }

        protected override void OnKeyUp(KeyCode keyCode)
        {
            KeyUp?.Invoke(keyCode);
        }
    }

    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        MyGlobalInputHookService _globalInputHookService;
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            _globalInputHookService = new MyGlobalInputHookService();
            _globalInputHookService.Start(SynchronizationContext.Current);

            MainWindow window = new MainWindow(_globalInputHookService);
            window.Show();
        }
    }
}
