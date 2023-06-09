namespace Atc.Installer.Wpf.ComponentProvider.PostgreSql;

public partial class PostgreSqlServerComponentProviderViewModel : ComponentProviderViewModel
{
    private readonly IPostgreSqlServerInstallerService pgInstallerService;
    private readonly IWindowsApplicationInstallerService waInstallerService;
    private string? testConnectionResult;

    public PostgreSqlServerComponentProviderViewModel(
        IPostgreSqlServerInstallerService postgreSqlServerInstallerService,
        INetworkShellService networkShellService,
        IWindowsApplicationInstallerService windowsApplicationInstallerService,
        ObservableCollectionEx<ComponentProviderViewModel> refComponentProviders,
        DirectoryInfo installerTempDirectory,
        DirectoryInfo installationDirectory,
        string projectName,
        IDictionary<string, object> defaultApplicationSettings,
        ApplicationOption applicationOption)
        : base(
            networkShellService,
            refComponentProviders,
            installerTempDirectory,
            installationDirectory,
            projectName,
            defaultApplicationSettings,
            applicationOption)
    {
        ArgumentNullException.ThrowIfNull(applicationOption);

        pgInstallerService = postgreSqlServerInstallerService ?? throw new ArgumentNullException(nameof(postgreSqlServerInstallerService));
        waInstallerService = windowsApplicationInstallerService ?? throw new ArgumentNullException(nameof(windowsApplicationInstallerService));
        PostgreSqlConnection = new PostgreSqlConnectionViewModel();

        InitializeFromApplicationOptions();
    }

    private void InitializeFromApplicationOptions()
    {
        InstalledMainFilePath = pgInstallerService.GetInstalledMainFile()?.FullName;
        ServiceName = pgInstallerService.GetServiceName();

        if (TryGetStringFromApplicationSettings("HostName", out var hostNameValue))
        {
            PostgreSqlConnection.HostName = ResolveTemplateIfNeededByApplicationSettingsLookup(hostNameValue);
        }

        if (TryGetUshortFromApplicationSettings("HostPort", out var hostPortValue))
        {
            PostgreSqlConnection.HostPort = hostPortValue;
        }

        if (TryGetStringFromApplicationSettings("Database", out var databaseValue))
        {
            PostgreSqlConnection.Database = ResolveTemplateIfNeededByApplicationSettingsLookup(databaseValue);
        }

        if (TryGetStringFromApplicationSettings("UserName", out var usernameValue))
        {
            PostgreSqlConnection.Username = ResolveTemplateIfNeededByApplicationSettingsLookup(usernameValue);
        }

        if (TryGetStringFromApplicationSettings("Password", out var passwordValue))
        {
            PostgreSqlConnection.Password = ResolveTemplateIfNeededByApplicationSettingsLookup(passwordValue);
        }

        TestConnectionResult = string.Empty;
    }

    public PostgreSqlConnectionViewModel PostgreSqlConnection { get; set; }

    public string? TestConnectionResult
    {
        get => testConnectionResult;
        set
        {
            testConnectionResult = value;
            RaisePropertyChanged();
        }
    }

    public override void CheckServiceState()
    {
        base.CheckServiceState();

        if (!pgInstallerService.IsInstalled)
        {
            return;
        }

        if (RunningState != ComponentRunningState.Stopped &&
            RunningState != ComponentRunningState.PartiallyRunning &&
            RunningState != ComponentRunningState.Running)
        {
            RunningState = ComponentRunningState.Checking;
        }

        var isRunning = pgInstallerService.IsRunning;
        RunningState = isRunning
            ? ComponentRunningState.Running
            : ComponentRunningState.Stopped;
    }

    public override bool TryGetStringFromApplicationSetting(
        string key,
        out string resultValue)
    {
        if ("ConnectionString".Equals(key, StringComparison.Ordinal))
        {
            var connectionString = PostgreSqlConnection.GetConnectionString();
            if (connectionString is not null)
            {
                resultValue = connectionString;
                return true;
            }
        }

        resultValue = string.Empty;
        return false;
    }

    public override bool CanServiceStopCommandHandler()
        => RunningState == ComponentRunningState.Running;

    public override async Task ServiceStopCommandHandler()
    {
        if (!CanServiceStopCommandHandler())
        {
            return;
        }

        IsBusy = true;

        LogItems.Add(LogItemFactory.CreateTrace("Stop"));

        var isStopped = await waInstallerService
            .StopService(ServiceName!)
            .ConfigureAwait(true);

        if (isStopped)
        {
            RunningState = ComponentRunningState.Stopped;
            LogAndSendToastNotificationMessage(
                ToastNotificationType.Information,
                Name,
                "Service is stopped");
        }
        else
        {
            LogAndSendToastNotificationMessage(
                ToastNotificationType.Error,
                Name,
                "Could not stop service");
        }

        IsBusy = false;
    }

    public override bool CanServiceStartCommandHandler()
        => RunningState == ComponentRunningState.Stopped;

    public override async Task ServiceStartCommandHandler()
    {
        if (!CanServiceStartCommandHandler())
        {
            return;
        }

        IsBusy = true;

        LogItems.Add(LogItemFactory.CreateTrace("Start"));

        var isStarted = await waInstallerService
            .StartService(ServiceName!)
            .ConfigureAwait(true);

        if (isStarted)
        {
            RunningState = ComponentRunningState.Running;
            LogAndSendToastNotificationMessage(
                ToastNotificationType.Information,
                Name,
                "Service is started");
        }
        else
        {
            LogAndSendToastNotificationMessage(
                ToastNotificationType.Error,
                Name,
                "Could not start service");
        }

        IsBusy = false;
    }
}