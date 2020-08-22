using BookmarkItCommonLibrary.Domain;
using BookmarkItCommonLibrary.Util;
using BookmarkItSyncLibrary;
using BookmarkItSyncLibrary.Domain;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Utilities.UseCase;
using Windows.ApplicationModel;
using Windows.ApplicationModel.AppService;
using Windows.ApplicationModel.Core;
using Windows.Foundation.Collections;
using Utilities.Extension;
using System.IO;

namespace SystemTrayComponent
{
    public sealed class SystemTrayApplicationContext : ApplicationContext
    {
        private AppServiceConnection connection = null;
        private NotifyIcon notifyIcon = null;

        public SystemTrayApplicationContext()
        {
            MenuItem syncMenuItem = new MenuItem("Sync Account", new EventHandler(SyncAccount));
            MenuItem exitMenuItem = new MenuItem("Exit", new EventHandler(Exit));

            notifyIcon = new NotifyIcon();
            notifyIcon.DoubleClick += new EventHandler(OpenApp);
            notifyIcon.Icon = Properties.Resources.Icon1;
            notifyIcon.ContextMenu = new ContextMenu(new MenuItem[] { syncMenuItem, exitMenuItem });
            notifyIcon.Visible = true;
            SendMessageToApp("TrayOpen");
        }

        private async Task SendMessageToApp(string message)
        {
            if (connection == null)
            {
                connection = new AppServiceConnection();
                connection.PackageFamilyName = Package.Current.Id.FamilyName;
                connection.AppServiceName = CommonConstants.AppServiceName;
                connection.ServiceClosed += OnServiceClosed;
                connection.RequestReceived += OnRequestReceived;
                AppServiceConnectionStatus connectionStatus = await connection.OpenAsync();
                if (connectionStatus != AppServiceConnectionStatus.Success)
                {
                    MessageBox.Show($"Status: {connectionStatus.ToString()}");
                    return;
                }
                var valueSet = new ValueSet();
                valueSet.Add(message, string.Empty);
                await connection.SendMessageAsync(valueSet);
            }
        }

        private async void OnRequestReceived(AppServiceConnection sender, AppServiceRequestReceivedEventArgs args)
        {
            try
            {
                var httpClient = new System.Net.Http.HttpClient();
                await httpClient.GetAsync("https://secure.splitwise.com/api/v3.0/get_currencies");
                System.Diagnostics.Debug.WriteLine(args.Request.Message.ToString());
                if (args.Request.Message.ContainsKey("ForceSync"))
                {
                    var jSyncRequest = JObject.Parse(args.Request.Message["ForceSync"].ToString());
                    var userId = jSyncRequest.GetString("UserId");
                    var dbPath = jSyncRequest.GetString("DBPath");
                    System.Diagnostics.Debug.WriteLine(jSyncRequest.ToString());
                    await httpClient.GetAsync("https://secure.splitwise.com/api/v3.0/get_currencies");
                    LogToFile(jSyncRequest.ToString());
                    SyncServiceManager.Instance.Initialize(CommonConstants.DBFileName, dbPath);
                    await httpClient.GetAsync("https://secure.splitwise.com/api/v3.0/get_currencies");
                    SyncAccountInternal(userId);
                }
            }
            catch (Exception ex)
            {
                LogToFile(ex.ToString());
            }
        }

        private void OnServiceClosed(AppServiceConnection sender, AppServiceClosedEventArgs args)
        {
            System.Diagnostics.Debug.WriteLine("Service closed");
            connection.ServiceClosed -= OnServiceClosed;
            connection = null;
        }

        private async void OpenApp(object sender, EventArgs e)
        {
            IEnumerable<AppListEntry> appListEntries = await Package.Current.GetAppListEntriesAsync();
            await appListEntries.First().LaunchAsync();
        }

        private void Exit(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private async void SyncAccount(object sender, EventArgs e)
        {
            try
            {
                var httpClient = new System.Net.Http.HttpClient();
                SyncServiceManager.Instance.Initialize(CommonConstants.DBFileName, Windows.Storage.ApplicationData.Current.LocalFolder.Path);
                await httpClient.GetAsync("https://secure.splitwise.com/api/v3.0/get_currencies?s=" + Windows.Storage.ApplicationData.Current.LocalFolder.Path);
                var getCurrentUserReq = new GetCurrentUserDetailsRequest();
                var getCurrentUserUC = new GetCurrentUserDetails(getCurrentUserReq, new GetCurrentUserDetailsCallback(this));
                getCurrentUserUC.Execute();
            }
            catch (Exception ex)
            {
                LogToFile(ex.ToString());
                System.Diagnostics.Debug.WriteLine(ex.ToString());
            }
        }

        private void LogToFile(string message)
        {
            var file = System.IO.File.Create(Windows.Storage.ApplicationData.Current.LocalFolder.Path + "\\win32log.txt");
            using (file)
            {
                var data = Encoding.UTF8.GetBytes(message.ToString());
                file.Write(data, 0, data.Length);
            }
        }

        private void SyncAccountInternal(string userId)
        {
            SyncServiceManager.Instance.Initialize(CommonConstants.DBFileName, Windows.Storage.ApplicationData.Current.LocalFolder.Path);
            var syncBookmarksReq = new SyncBookmarksRequest(userId);
            var syncBookmarksUC = new SyncBookmarks(syncBookmarksReq, new SyncBookmarksCallback(this));
            syncBookmarksUC.Execute();
        }

        class GetCurrentUserDetailsCallback : IGetCurrentUserDetailsPresenterCallback
        {
            private readonly SystemTrayApplicationContext Presenter;

            public GetCurrentUserDetailsCallback(SystemTrayApplicationContext presenter)
            {
                Presenter = presenter;
            }

            public void OnCanceled(IUseCaseResponse<GetCurrentUserDetailsResponse> response)
            {

            }

            public void OnError(UseCaseError error)
            {
                System.Diagnostics.Debug.WriteLine("error in fetching userdetails");
            }

            public void OnFailed(IUseCaseResponse<GetCurrentUserDetailsResponse> response)
            {

            }

            public void OnSuccess(IUseCaseResponse<GetCurrentUserDetailsResponse> response)
            {
                Presenter.SyncAccountInternal(response.Data.User.Id);
            }
        }

        class SyncBookmarksCallback : ISyncBookmarksPresenterCallback
        {
            private readonly SystemTrayApplicationContext Presenter;

            public SyncBookmarksCallback(SystemTrayApplicationContext presenter)
            {
                Presenter = presenter;
            }

            public void OnCanceled(IUseCaseResponse<SyncBookmarksResponse> response)
            {

            }

            public void OnError(UseCaseError error)
            {

            }

            public void OnFailed(IUseCaseResponse<SyncBookmarksResponse> response)
            {

            }

            public void OnProgress(IUseCaseResponse<SyncBookmarksResponse> response)
            {
            }

            public async void OnSuccess(IUseCaseResponse<SyncBookmarksResponse> response)
            {
                await Presenter.SendMessageToApp("Sync Complete");
            }
        }
    }
}