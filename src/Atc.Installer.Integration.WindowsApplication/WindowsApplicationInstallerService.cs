// ReSharper disable LoopCanBeConvertedToQuery
namespace Atc.Installer.Integration.WindowsApplication;

[SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "OK.")]
public sealed class WindowsApplicationInstallerService : IWindowsApplicationInstallerService
{
    private static readonly object InstanceLock = new();
    private static WindowsApplicationInstallerService? instance;

    private WindowsApplicationInstallerService()
    {
    }

    public static WindowsApplicationInstallerService Instance
    {
        get
        {
            lock (InstanceLock)
            {
                return instance ??= new WindowsApplicationInstallerService();
            }
        }
    }

    public bool IsMicrosoftDonNetFramework48()
        => InstalledAppsInstallerService.Instance.IsMicrosoftDonNetFramework48();

    public bool IsMicrosoftDonNet7()
        => InstalledAppsInstallerService.Instance.IsMicrosoftDonNet7();

    public ComponentRunningState GetServiceState(
        string serviceName)
    {
        try
        {
            var services = ServiceController.GetServices();
            var service =
                services.FirstOrDefault(x => x.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase));
            if (service is null)
            {
                return ComponentRunningState.NotAvailable;
            }

            return service.Status switch
            {
                ServiceControllerStatus.ContinuePending => ComponentRunningState.Checking,
                ServiceControllerStatus.Paused => ComponentRunningState.Stopped,
                ServiceControllerStatus.PausePending => ComponentRunningState.Checking,
                ServiceControllerStatus.Running => ComponentRunningState.Running,
                ServiceControllerStatus.StartPending => ComponentRunningState.Checking,
                ServiceControllerStatus.Stopped => ComponentRunningState.Stopped,
                ServiceControllerStatus.StopPending => ComponentRunningState.Checking,
                _ => throw new SwitchExpressionException(service.Status),
            };
        }
        catch
        {
            return ComponentRunningState.Unknown;
        }
    }

    public async Task<bool> StopService(
        string serviceName,
        ushort timeoutInSeconds = 60)
    {
        try
        {
            var services = ServiceController.GetServices();
            var service =
                services.FirstOrDefault(x => x.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase));
            if (service is null ||
                service.Status != ServiceControllerStatus.Running)
            {
                return false;
            }

            await Task
                .Run(() =>
                {
                    service.Stop();
                    service.WaitForStatus(
                        ServiceControllerStatus.Stopped,
                        TimeSpan.FromSeconds(timeoutInSeconds));
                })
                .ConfigureAwait(false);

            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> StartService(
        string serviceName,
        ushort timeoutInSeconds = 60)
    {
        try
        {
            var services = ServiceController.GetServices();
            var service =
                services.FirstOrDefault(x => x.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase));
            if (service is null ||
                service.Status != ServiceControllerStatus.Stopped)
            {
                return false;
            }

            await Task
                .Run(() =>
                {
                    service.Start();
                    service.WaitForStatus(
                        ServiceControllerStatus.Running,
                        TimeSpan.FromSeconds(timeoutInSeconds));
                })
                .ConfigureAwait(false);

            return true;
        }
        catch
        {
            return false;
        }
    }

    public ComponentRunningState GetApplicationState(
        string applicationName)
    {
        try
        {
            foreach (var process in Process.GetProcesses())
            {
                if (process.ProcessName.Equals(applicationName, StringComparison.OrdinalIgnoreCase))
                {
                    return ComponentRunningState.Running;
                }
            }

            return ComponentRunningState.NotAvailable;
        }
        catch
        {
            return ComponentRunningState.Unknown;
        }
    }

    public bool StopApplication(
        string applicationName,
        ushort timeoutInSeconds = 60)
    {
        ArgumentNullException.ThrowIfNullOrEmpty(applicationName);

        try
        {
            var result = false;
            foreach (var process in Process.GetProcesses())
            {
                if (!process.ProcessName.Equals(applicationName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                process.Kill(entireProcessTree: true);
                process.WaitForExit(TimeSpan.FromSeconds(timeoutInSeconds));
                process.Dispose();
                result = true;
            }

            return result;
        }
        catch
        {
            return false;
        }

        return false;
    }

    public bool StartApplication(
        string applicationName,
        ushort timeoutInSeconds = 60)
    {
        ArgumentNullException.ThrowIfNullOrEmpty(applicationName);

        try
        {
            Process.Start(applicationName);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool StartApplication(
        FileInfo applicationFile,
        ushort timeoutInSeconds = 60)
    {
        ArgumentNullException.ThrowIfNull(applicationFile);

        try
        {
            Process.Start(applicationFile.FullName);
            return true;
        }
        catch
        {
            return false;
        }
    }
}