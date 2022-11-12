using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Threading.Channels;
using System.Threading;
using static GlobalInputHook.Interop.Interop;

namespace GlobalInputHook
{
    public abstract class GlobalInputHookService : IDisposable
    {
        #region Private Members
        private bool _isStarted = false;

        private Channel<KeyPress>? _keyPressChannel;

        private IntPtr _keyboardHookId = IntPtr.Zero;
        private IntPtr _mouseHookId = IntPtr.Zero;

        private bool _disposed = false;

        private CancellationTokenSource? _cancellationTokenSource;

        private Task? _keyListenerTask;

        private SynchronizationContext? _syncCtx;

        private HookProc? _keyboardHookProcDelegate;
        private HookProc? _mouseHookProcDelegate;
        #endregion

        #region Public API
        /// <summary>
        /// Starts the low-level keyboard hook.
        /// Note: This must be called from a thread that has a Win32 message loop (i.e. UI thread of UI app).
        /// Make sure to call Stop() or Dispose() in order to unhook low-level keyboard hook.
        /// </summary>
        /// <param name="synchronizationContext">
        /// The SynchronizationContext to invoke key press methods and OnError method on, if null the methods are invoked
        /// on the thread pool.
        /// </param>
        /// <exception cref="PlatformNotSupportedException"/>
        /// <exception cref="ObjectDisposedException"/>
        public void Start(SynchronizationContext? syncCtx = null)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                throw new PlatformNotSupportedException("Windows is the only supported platform.");
            }

            ThrowIfDisposed();

            if (_isStarted)
                return;

            _isStarted = true;

            _keyPressChannel = Channel.CreateUnbounded<KeyPress>();
            _cancellationTokenSource = new CancellationTokenSource();
            _syncCtx = syncCtx;

            _keyboardHookProcDelegate = KeyboardHookCallback;
            _mouseHookProcDelegate = MouseHookCallback;

            _keyListenerTask = Task.Run(() => RunKeyListenerAsync(_cancellationTokenSource.Token));

            _keyboardHookId = SetHook(WH_KEYBOARD_LL, _keyboardHookProcDelegate);
            _mouseHookId = SetHook(WH_MOUSE_LL, _mouseHookProcDelegate);
        }

        /// <summary>
        /// Stops the low-level keyboard hook synchronously.
        /// </summary>
        /// <exception cref="ObjectDisposedException"></exception>
        public void Stop()
        {
            ThrowIfDisposed();

            if (!_isStarted)
                return;

            StopAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Stops the low-level input hook asynchronously.
        /// </summary>
        /// <exception cref="ObjectDisposedException"></exception>
        public async Task StopAsync()
        {
            ThrowIfDisposed();

            if (!_isStarted)
            {
                return;
            }

            _ = UnhookWindowsHookEx(_keyboardHookId);
            _ = UnhookWindowsHookEx(_mouseHookId);

            _keyPressChannel!.Writer.Complete();
            _cancellationTokenSource!.Cancel();

            try
            {
                await _keyListenerTask!.ConfigureAwait(false);
            }
            finally
            {
                _cancellationTokenSource.Dispose();
                _cancellationTokenSource = null;
            }
            _keyListenerTask = null;

            _keyPressChannel = null;
            _syncCtx = null;
            _keyboardHookProcDelegate = null;
            _mouseHookProcDelegate = null;

            _isStarted = false;
        }
        #endregion

        #region Protected Methods
        /// <summary>
        /// This method is invoked when an error occurs with processing input.
        /// </summary>
        /// <param name="exception">
        /// The exception that occured while processing input.
        /// </param>
        protected abstract void OnError(Exception exception);

        /// <summary>
        /// This method is invoked when a key is pressed down.
        /// </summary>
        /// <param name="keyCode">
        /// The specific key that was pressed.
        /// </param>
        protected abstract void OnKeyDown(KeyCode keyCode);

        /// <summary>
        /// This method is invoked when a key is released.
        /// </summary>
        /// <param name="keyCode">
        /// The specific key that was released.
        /// </param>
        protected abstract void OnKeyUp(KeyCode keyCode);
        #endregion

        #region Private Methods
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AddKeyPressToQueue(int wParamVal, int vkCode)
        {
            try
            {
                KeyPress keyPress = default;
                bool addToQueue = true;

                if (wParamVal == WM_KEYDOWN || wParamVal == WM_SYSKEYDOWN)
                {
                    keyPress.KeyPressType = KeyPressType.Down;
                    keyPress.VkCode = vkCode;
                }
                else if (wParamVal == WM_KEYUP || wParamVal == WM_SYSKEYUP)
                {
                    keyPress.KeyPressType = KeyPressType.Up;
                    keyPress.VkCode = vkCode;
                }
                else
                {
                    addToQueue = false;
                }

                if (addToQueue)
                {
                    _ = _keyPressChannel!.Writer.TryWrite(keyPress);
                }
            }
            catch (Exception ex)
            {
                InvokeOnErrorOnSyncCtxIfExists(ex);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AddMousePressToQueue(int wParamVal, MSLLHOOKSTRUCT mouseStruct)
        {
            try
            {
                KeyPress keyPress = default;
                bool addToQueue = true;

                if (wParamVal == WM_LBUTTONDOWN)
                {
                    keyPress.KeyPressType = KeyPressType.Down;
                    keyPress.VkCode = (int)KeyCode.LeftMouse;
                }
                else if (wParamVal == WM_LBUTTONUP)
                {
                    keyPress.KeyPressType = KeyPressType.Up;
                    keyPress.VkCode = (int)KeyCode.LeftMouse;
                }
                else if (wParamVal == WM_RBUTTONDOWN)
                {
                    keyPress.KeyPressType = KeyPressType.Down;
                    keyPress.VkCode = (int)KeyCode.RightMouse;
                }
                else if (wParamVal == WM_RBUTTONUP)
                {
                    keyPress.KeyPressType = KeyPressType.Up;
                    keyPress.VkCode = (int)KeyCode.RightMouse;
                }
                else if (wParamVal == WM_MBUTTONDOWN)
                {
                    keyPress.KeyPressType = KeyPressType.Down;
                    keyPress.VkCode = (int)KeyCode.MiddleMouse;
                }
                else if (wParamVal == WM_MBUTTONUP)
                {
                    keyPress.KeyPressType = KeyPressType.Up;
                    keyPress.VkCode = (int)KeyCode.MiddleMouse;
                }
                else if (wParamVal == WM_XBUTTONDOWN || wParamVal == WM_NCXBUTTONDOWN)
                {
                    keyPress.KeyPressType = KeyPressType.Down;

                    var buttonVal = HiWord(mouseStruct.mouseData);

                    if (buttonVal == 1)
                    {
                        keyPress.VkCode = (int)KeyCode.Mouse4;
                    }
                    else if (buttonVal == 2)
                    {
                        keyPress.VkCode = (int)KeyCode.Mouse5;
                    }
                    else
                    {
                        addToQueue = false;
                    }
                }
                else if (wParamVal == WM_XBUTTONUP || wParamVal == WM_NCXBUTTONUP)
                {
                    keyPress.KeyPressType = KeyPressType.Up;

                    var buttonVal = HiWord(mouseStruct.mouseData);

                    if (buttonVal == 1)
                    {
                        keyPress.VkCode = (int)KeyCode.Mouse4;
                    }
                    else if (buttonVal == 2)
                    {
                        keyPress.VkCode = (int)KeyCode.Mouse5;
                    }
                    else
                    {
                        addToQueue = false;
                    }
                }
                else if (wParamVal == WM_MOUSEWHEEL)
                {
                    var wheelVal = HiWordSigned(mouseStruct.mouseData);

                    keyPress.KeyPressType = KeyPressType.Down;
                    if (wheelVal < 0)
                    {
                        keyPress.VkCode = (int)KeyCode.MouseScrollDown;
                    }
                    else if (wheelVal > 0)
                    {
                        keyPress.VkCode = (int)KeyCode.MouseScrollUp;
                    }
                    else
                    {
                        addToQueue = false;
                    }
                }
                else
                {
                    addToQueue = false;
                }

                if (addToQueue)
                {
                    _ = _keyPressChannel!.Writer.TryWrite(keyPress);
                }
            }
            catch (Exception ex)
            {
                InvokeOnErrorOnSyncCtxIfExists(ex);
            }
        }

        private async Task RunKeyListenerAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                while (await _keyPressChannel!.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    HandleInput();
                }
            }
            catch (Exception e)
            {
                if (e is not OperationCanceledException)
                {
                    InvokeOnErrorOnSyncCtxIfExists(e);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void HandleInput()
        {
            if (_keyPressChannel!.Reader.TryRead(out KeyPress keyPress))
            {
                if (keyPress.KeyPressType == KeyPressType.Down)
                {
                    if (_syncCtx is null)
                    {
                        var key = (KeyCode)keyPress.VkCode;
                        OnKeyDown(key);
                        if (key == KeyCode.MouseScrollDown || key == KeyCode.MouseScrollUp)
                        {
                            OnKeyUp(key);
                        }
                    }
                    else
                    {
                        _syncCtx.Post((d) =>
                        {
                            var key = (KeyCode)keyPress.VkCode;
                            OnKeyDown(key);
                            if (key == KeyCode.MouseScrollDown || key == KeyCode.MouseScrollUp)
                            {
                                OnKeyUp(key);
                            }
                        }, null);
                    }
                }
                else
                {
                    if (_syncCtx is null)
                    {
                        OnKeyUp((KeyCode)keyPress.VkCode);
                    }
                    else
                    {
                        _syncCtx.Post((d) => OnKeyUp((KeyCode)keyPress.VkCode), null);
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void InvokeOnErrorOnSyncCtxIfExists(Exception exception)
        {
            if (_syncCtx is null)
            {
                OnError(exception);
            }
            else
            {
                _syncCtx.Post((d) => OnError(exception), null);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(GlobalInputHookService));
            }
        }
        #endregion

        #region Low Level
        private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                int wParamVal = wParam.ToInt32();
                _ = Task.Run(() => AddKeyPressToQueue(wParamVal, vkCode));
            }

            return CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);
        }

        private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                MSLLHOOKSTRUCT mouseStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                int wParamVal = wParam.ToInt32();
                _ = Task.Run(() => AddMousePressToQueue(wParamVal, mouseStruct));
            }

            return CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);
        }
        #endregion

        #region IDisposable
        ~GlobalInputHookService() => Dispose(false);

        // Public implementation of Dispose pattern callable by consumers.
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        // Protected implementation of Dispose pattern.
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    if (_isStarted)
                    {
                        Stop();
                    }
                }

                if (_isStarted)
                {
                    _cancellationTokenSource?.Cancel();
                    _keyListenerTask!.GetAwaiter().GetResult();
                    _cancellationTokenSource?.Dispose();
                    _ = UnhookWindowsHookEx(_keyboardHookId);
                    _ = UnhookWindowsHookEx(_mouseHookId);
                }

                _disposed = true;
            }
        }
        #endregion
    }
}