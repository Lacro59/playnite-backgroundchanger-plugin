using CommonPluginsShared;
using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation;

namespace BackgroundChanger.Controls
{
    /// <summary>
    /// Registers <see cref="Window.UnloadedEvent"/> once and dispatches theme property sync
    /// to all live <see cref="IMediaThemeSyncTarget"/> instances when Playnite settings close.
    /// </summary>
    internal interface IMediaThemeSyncTarget
    {
        /// <summary>Copies native theme control properties onto the plugin media control.</summary>
        void SyncThemePropertiesOnSettingsClose();
    }

    /// <summary>
    /// Static coordinator for theme sync on <c>WindowSettings</c> unload (single class handler).
    /// </summary>
    internal static class MediaThemeSyncWindowHandler
    {
        private static readonly object SyncRoot = new object();
        private static readonly List<WeakReference<IMediaThemeSyncTarget>> Targets = new List<WeakReference<IMediaThemeSyncTarget>>();
        private static bool _handlerRegistered;

        /// <summary>
        /// Tracks a live control instance and ensures the global window handler is registered once.
        /// </summary>
        public static void EnsureRegistered(IMediaThemeSyncTarget target)
        {
            if (target == null)
            {
                return;
            }

            Register(target);

            lock (SyncRoot)
            {
                if (_handlerRegistered)
                {
                    return;
                }

                if (API.Instance?.ApplicationInfo?.Mode != ApplicationMode.Desktop)
                {
                    return;
                }

                EventManager.RegisterClassHandler(
                    typeof(Window),
                    Window.UnloadedEvent,
                    new RoutedEventHandler(OnWindowUnloaded));

                _handlerRegistered = true;
                Common.LogDebug(true, "[MediaThemeSync] Window.UnloadedEvent class handler registered once");
            }
        }

        /// <summary>Stops tracking an instance (call from control unload).</summary>
        public static void Unregister(IMediaThemeSyncTarget target)
        {
            if (target == null)
            {
                return;
            }

            lock (SyncRoot)
            {
                PurgeDeadTargets();

                for (int i = Targets.Count - 1; i >= 0; i--)
                {
                    if (Targets[i].TryGetTarget(out IMediaThemeSyncTarget existing) && ReferenceEquals(existing, target))
                    {
                        Targets.RemoveAt(i);
                    }
                }
            }
        }

        private static void Register(IMediaThemeSyncTarget target)
        {
            lock (SyncRoot)
            {
                PurgeDeadTargets();

                foreach (WeakReference<IMediaThemeSyncTarget> reference in Targets)
                {
                    if (reference.TryGetTarget(out IMediaThemeSyncTarget existing) && ReferenceEquals(existing, target))
                    {
                        return;
                    }
                }

                Targets.Add(new WeakReference<IMediaThemeSyncTarget>(target));
            }
        }

        private static void OnWindowUnloaded(object sender, EventArgs e)
        {
            string winIdProperty = string.Empty;
            string winName = string.Empty;

            try
            {
                Window window = sender as Window;
                if (window == null)
                {
                    return;
                }

                winIdProperty = window.GetValue(AutomationProperties.AutomationIdProperty)?.ToString() ?? string.Empty;
                winName = window.Name ?? string.Empty;

                if (winIdProperty != "WindowSettings")
                {
                    return;
                }

                List<IMediaThemeSyncTarget> aliveTargets = GetAliveTargets();
                Common.LogDebug(
                    true,
                    string.Format(
                        "[MediaThemeSync] WindowSettings unloaded, dispatching ThemeSync to {0} instance(s)",
                        aliveTargets.Count));

                foreach (IMediaThemeSyncTarget target in aliveTargets)
                {
                    target.SyncThemePropertiesOnSettingsClose();
                }
            }
            catch (Exception ex)
            {
                Common.LogError(
                    ex,
                    false,
                    string.Format("Error on MediaThemeSync Window.Unloaded for {0} - {1}", winName, winIdProperty),
                    true,
                    BackgroundChanger.PluginDatabase?.PluginName ?? "BackgroundChanger");
            }
        }

        private static List<IMediaThemeSyncTarget> GetAliveTargets()
        {
            List<IMediaThemeSyncTarget> aliveTargets = new List<IMediaThemeSyncTarget>();

            lock (SyncRoot)
            {
                PurgeDeadTargets();

                foreach (WeakReference<IMediaThemeSyncTarget> reference in Targets)
                {
                    if (reference.TryGetTarget(out IMediaThemeSyncTarget target))
                    {
                        aliveTargets.Add(target);
                    }
                }
            }

            return aliveTargets;
        }

        private static void PurgeDeadTargets()
        {
            for (int i = Targets.Count - 1; i >= 0; i--)
            {
                if (!Targets[i].TryGetTarget(out IMediaThemeSyncTarget _))
                {
                    Targets.RemoveAt(i);
                }
            }
        }
    }
}
