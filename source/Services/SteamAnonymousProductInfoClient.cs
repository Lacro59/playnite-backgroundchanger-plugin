using CommonPluginsShared;
using SteamKit2;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BackgroundChanger.Services
{
    /// <summary>
    /// Fetches Steam product info via anonymous SteamKit login (Universal Steam Metadata pattern).
    /// </summary>
    internal sealed class SteamAnonymousProductInfoClient
    {
        private const string LogPrefix = "[SteamAnonymousProductInfoClient]";
        private const int TimeoutMs = 10000;

        /// <summary>
        /// Retrieves app product info key-values for <paramref name="appId"/>.
        /// </summary>
        /// <param name="appId">Steam AppId.</param>
        /// <returns>Product info, or <c>null</c> on connect/logon/PICS failure.</returns>
        public KeyValue GetProductInfo(uint appId)
        {
            if (appId == 0)
            {
                return null;
            }

            SteamClient steamClient = new SteamClient();
            CallbackManager manager = new CallbackManager(steamClient);
            SteamUser steamUser = steamClient.GetHandler<SteamUser>();
            SteamApps steamApps = steamClient.GetHandler<SteamApps>();

            bool isRunning = true;
            AutoResetEvent connectedEvent = new AutoResetEvent(false);
            EResult connectedResult = EResult.Fail;
            AutoResetEvent loggedOnEvent = new AutoResetEvent(false);
            EResult loggedOnResult = EResult.Fail;

            manager.Subscribe<SteamClient.ConnectedCallback>(callback =>
            {
                connectedResult = callback.Result;
                connectedEvent.Set();
            });

            manager.Subscribe<SteamUser.LoggedOnCallback>(callback =>
            {
                loggedOnResult = callback.Result;
                loggedOnEvent.Set();
            });

            Task callbackTask = Task.Run(() =>
            {
                while (isRunning)
                {
                    manager.RunWaitCallbacks(TimeSpan.FromSeconds(1));
                }
            });

            try
            {
                steamClient.Connect();
                if (!connectedEvent.WaitOne(TimeoutMs) || connectedResult != EResult.OK)
                {
                    Common.LogDebug(false, LogPrefix + " Connect failed or timed out appId=" + appId);
                    return null;
                }

                steamUser.LogOnAnonymous(new SteamUser.AnonymousLogOnDetails());
                if (!loggedOnEvent.WaitOne(TimeoutMs) || loggedOnResult != EResult.OK)
                {
                    Common.LogDebug(false, LogPrefix + " Anonymous logon failed appId=" + appId);
                    return null;
                }

                AsyncJobMultiple<SteamApps.PICSProductInfoCallback> productJob = steamApps.PICSGetProductInfo(appId, null, false);
                Task<AsyncJobMultiple<SteamApps.PICSProductInfoCallback>.ResultSet> productTask = productJob.ToTask();
                if (!productTask.Wait(TimeoutMs))
                {
                    Common.LogDebug(false, LogPrefix + " PICS timeout appId=" + appId);
                    return null;
                }

                AsyncJobMultiple<SteamApps.PICSProductInfoCallback>.ResultSet resultSet = productTask.Result;
                SteamApps.PICSProductInfoCallback productInfo = null;
                if (resultSet != null && resultSet.Complete)
                {
                    productInfo = resultSet.Results.FirstOrDefault();
                }
                else if (resultSet != null)
                {
                    productInfo = resultSet.Results.FirstOrDefault(
                        prodCallback => prodCallback.Apps != null && prodCallback.Apps.ContainsKey(appId));
                }

                if (productInfo == null || !productInfo.Apps.ContainsKey(appId))
                {
                    Common.LogDebug(false, LogPrefix + " PICS missing app entry appId=" + appId);
                    return null;
                }

                Common.LogDebug(false, LogPrefix + " PICS success appId=" + appId);
                return productInfo.Apps[appId].KeyValues;
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, BackgroundChanger.PluginDatabase.PluginName);
                return null;
            }
            finally
            {
                isRunning = false;
                try
                {
                    steamClient.Disconnect();
                }
                catch
                {
                    // Best-effort cleanup after anonymous session.
                }

                try
                {
                    callbackTask.Wait(2000);
                }
                catch
                {
                    // Callback loop may still be shutting down.
                }
            }
        }
    }
}
