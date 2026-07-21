using CommonPluginsShared.Controls;
using Playnite.SDK;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;

namespace BackgroundChangerPlugin.Controls
{
    /// <summary>
    /// Shared lifecycle orchestration for media theme controls.
    /// Centralizes visibility/focus hooks and window activation transitions.
    /// </summary>
    public abstract class PluginMediaLifecycleControlBase : PluginUserControlExtend
    {
        private bool _applicationFocusEventsAttached;

        protected bool WindowsIsActivated { get; private set; } = true;

        /// <summary>
        /// Theme mirror flags updated on every game switch via SetThemesResources.
        /// They must not reset media controls or restart the settings debounce path.
        /// </summary>
        protected override bool ShouldApplySettingsProperty(string propertyName)
        {
            if (string.IsNullOrEmpty(propertyName))
            {
                return true;
            }

            if (propertyName == "HasDataBackground"
                || propertyName == "HasDataCover"
                || propertyName == "HasDataIcon")
            {
                return false;
            }

            return base.ShouldApplySettingsProperty(propertyName);
        }

        protected void InitializeMediaLifecycleHooks()
        {
            IsVisibleChanged += OnMediaLifecycleVisibilityChanged;
            Unloaded += OnMediaLifecycleUnloaded;
            DependencyPropertyDescriptor.FromProperty(
                PluginUserControlExtendBase.MustDisplayProperty,
                typeof(PluginUserControlExtendBase))
                .AddValueChanged(this, OnMustDisplayLifecycleChanged);

            LogControlTrace("Media lifecycle hooks initialized");
        }

        /// <summary>
        /// Whether media timers and playback are allowed for this control instance.
        /// Must be called on the control dispatcher thread (reads WPF dependency properties).
        /// </summary>
        protected bool IsLifecycleDisplayActive()
        {
            return MustDisplay
                && Visibility == Visibility.Visible
                && WindowsIsActivated
                && IsLoaded;
        }

        /// <summary>
        /// Marshals an action to the UI thread and runs it only when the media lifecycle is active.
        /// Safe to call from <see cref="System.Timers.Timer"/> callbacks.
        /// </summary>
        protected void InvokeOnUiIfLifecycleActive(Action action)
        {
            if (action == null)
            {
                return;
            }

            Dispatcher dispatcher = API.Instance?.MainView?.UIDispatcher ?? Dispatcher;
            if (dispatcher == null)
            {
                return;
            }

            _ = dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
            {
                if (!IsLifecycleDisplayActive())
                {
                    return;
                }

                action();
            }));
        }

        protected void AttachApplicationFocusEvents()
        {
            if (_applicationFocusEventsAttached || Application.Current == null)
            {
                return;
            }

            Application.Current.Activated += OnApplicationActivated;
            Application.Current.Deactivated += OnApplicationDeactivated;
            if (Application.Current.MainWindow != null)
            {
                Application.Current.MainWindow.StateChanged += OnMainWindowStateChanged;
            }

            _applicationFocusEventsAttached = true;
            LogControlTrace("Application focus events attached");
        }

        private void DetachApplicationFocusEvents()
        {
            if (!_applicationFocusEventsAttached || Application.Current == null)
            {
                return;
            }

            Application.Current.Activated -= OnApplicationActivated;
            Application.Current.Deactivated -= OnApplicationDeactivated;
            if (Application.Current.MainWindow != null)
            {
                Application.Current.MainWindow.StateChanged -= OnMainWindowStateChanged;
            }

            _applicationFocusEventsAttached = false;
        }

        private void OnMediaLifecycleVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            NotifyMediaLifecycleStateChanged("visibility-changed");
        }

        private void OnMustDisplayLifecycleChanged(object sender, EventArgs e)
        {
            NotifyMediaLifecycleStateChanged("must-display-changed");
        }

        private void OnMediaLifecycleUnloaded(object sender, RoutedEventArgs e)
        {
            LogControlTrace("Media lifecycle unloaded");
            OnMediaLifecycleUnloadedCore();
            DetachApplicationFocusEvents();
        }

        private void OnApplicationActivated(object sender, EventArgs e)
        {
            ApplyWindowActivationState(true);
        }

        private void OnApplicationDeactivated(object sender, EventArgs e)
        {
            ApplyWindowActivationState(false);
        }

        private void OnMainWindowStateChanged(object sender, EventArgs e)
        {
            Window window = sender as Window;
            if (window == null)
            {
                return;
            }

            switch (window.WindowState)
            {
                case WindowState.Normal:
                case WindowState.Maximized:
                    OnApplicationActivated(sender, e);
                    break;
                case WindowState.Minimized:
                    OnApplicationDeactivated(sender, e);
                    break;
                default:
                    break;
            }
        }

        private void ApplyWindowActivationState(bool isActivated)
        {
            Action applyAction = () =>
            {
                if (WindowsIsActivated == isActivated)
                {
                    return;
                }

                WindowsIsActivated = isActivated;
                NotifyMediaLifecycleStateChanged(isActivated ? "window-activated" : "window-deactivated");
            };

            Dispatcher dispatcher = API.Instance?.MainView?.UIDispatcher ?? Dispatcher;
            if (dispatcher == null)
            {
                applyAction();
                return;
            }

            if (dispatcher.CheckAccess())
            {
                applyAction();
            }
            else
            {
                _ = dispatcher.BeginInvoke(DispatcherPriority.Normal, applyAction);
            }
        }

        protected virtual void OnMediaLifecycleUnloadedCore()
        {
        }

        private void NotifyMediaLifecycleStateChanged(string trigger)
        {
            LogControlTrace(
                "Media lifecycle state",
                string.Format(
                    "trigger={0}, active={1}, mustDisplay={2}, visibility={3}, windowActive={4}, isLoaded={5}",
                    trigger,
                    IsLifecycleDisplayActive(),
                    MustDisplay,
                    Visibility,
                    WindowsIsActivated,
                    IsLoaded));

            OnMediaLifecycleStateChanged();
        }

        protected abstract void OnMediaLifecycleStateChanged();
    }
}
