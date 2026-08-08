using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.ServiceProcess;

namespace UndefinedSS.ServicesPrechecker
{
    internal interface IServiceOperations
    {
        int? ReadStartType(string serviceName);
        void ChangeStartType(string serviceName, int startType);
        ServiceControllerStatus GetStatus(string serviceName);
        void StartAndWaitForRunning(string serviceName, TimeSpan timeout);
    }

    internal sealed class WindowsServiceOperations : IServiceOperations
    {
        private const uint ScManagerConnect = 0x0001;
        private const uint ServiceQueryConfig = 0x0001;
        private const uint ServiceChangeConfig = 0x0002;
        private const uint ServiceNoChange = 0xFFFFFFFF;
        private const int ErrorInsufficientBuffer = 122;
        private const int ErrorServiceDoesNotExist = 1060;

        public int? ReadStartType(string serviceName)
        {
            IntPtr managerHandle = OpenSCManager(null, null, ScManagerConnect);
            if (managerHandle == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            try
            {
                IntPtr serviceHandle = OpenService(
                    managerHandle,
                    serviceName,
                    ServiceQueryConfig);
                if (serviceHandle == IntPtr.Zero)
                {
                    int openError = Marshal.GetLastWin32Error();
                    if (openError == ErrorServiceDoesNotExist)
                    {
                        return null;
                    }

                    throw new Win32Exception(openError);
                }

                try
                {
                    uint bytesNeeded;
                    QueryServiceConfig(
                        serviceHandle,
                        IntPtr.Zero,
                        0,
                        out bytesNeeded);
                    int queryError = Marshal.GetLastWin32Error();
                    if (queryError != ErrorInsufficientBuffer || bytesNeeded == 0)
                    {
                        throw new Win32Exception(queryError);
                    }

                    IntPtr buffer = Marshal.AllocHGlobal(Convert.ToInt32(bytesNeeded));
                    try
                    {
                        if (!QueryServiceConfig(
                            serviceHandle,
                            buffer,
                            bytesNeeded,
                            out bytesNeeded))
                        {
                            throw new Win32Exception(Marshal.GetLastWin32Error());
                        }

                        QueryServiceConfigData config =
                            (QueryServiceConfigData)Marshal.PtrToStructure(
                                buffer,
                                typeof(QueryServiceConfigData));
                        return Convert.ToInt32(config.StartType);
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(buffer);
                    }
                }
                finally
                {
                    CloseServiceHandle(serviceHandle);
                }
            }
            finally
            {
                CloseServiceHandle(managerHandle);
            }
        }

        public void ChangeStartType(string serviceName, int startType)
        {
            IntPtr managerHandle = OpenSCManager(null, null, ScManagerConnect);
            if (managerHandle == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            try
            {
                IntPtr serviceHandle = OpenService(
                    managerHandle,
                    serviceName,
                    ServiceChangeConfig);
                if (serviceHandle == IntPtr.Zero)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                try
                {
                    if (!ChangeServiceConfig(
                        serviceHandle,
                        ServiceNoChange,
                        Convert.ToUInt32(startType),
                        ServiceNoChange,
                        null,
                        null,
                        IntPtr.Zero,
                        null,
                        null,
                        null,
                        null))
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                    }
                }
                finally
                {
                    CloseServiceHandle(serviceHandle);
                }
            }
            finally
            {
                CloseServiceHandle(managerHandle);
            }
        }

        public ServiceControllerStatus GetStatus(string serviceName)
        {
            using (ServiceController controller = new ServiceController(serviceName))
            {
                controller.Refresh();
                return controller.Status;
            }
        }

        public void StartAndWaitForRunning(string serviceName, TimeSpan timeout)
        {
            using (ServiceController controller = new ServiceController(serviceName))
            {
                controller.Refresh();
                ServiceControllerStatus status = controller.Status;

                if (status == ServiceControllerStatus.Running)
                {
                    return;
                }

                if (status == ServiceControllerStatus.StartPending ||
                    status == ServiceControllerStatus.ContinuePending)
                {
                    controller.WaitForStatus(ServiceControllerStatus.Running, timeout);
                }
                else if (status == ServiceControllerStatus.Paused)
                {
                    controller.Continue();
                    controller.WaitForStatus(ServiceControllerStatus.Running, timeout);
                }
                else if (status == ServiceControllerStatus.PausePending)
                {
                    controller.WaitForStatus(ServiceControllerStatus.Paused, timeout);
                    controller.Continue();
                    controller.WaitForStatus(ServiceControllerStatus.Running, timeout);
                }
                else
                {
                    if (status == ServiceControllerStatus.StopPending)
                    {
                        controller.WaitForStatus(ServiceControllerStatus.Stopped, timeout);
                    }

                    controller.Start();
                    controller.WaitForStatus(ServiceControllerStatus.Running, timeout);
                }

                controller.Refresh();
                if (controller.Status != ServiceControllerStatus.Running)
                {
                    throw new InvalidOperationException("服务未进入正在运行状态。");
                }
            }
        }

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr OpenSCManager(
            string machineName,
            string databaseName,
            uint desiredAccess);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr OpenService(
            IntPtr serviceControlManager,
            string serviceName,
            uint desiredAccess);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ChangeServiceConfig(
            IntPtr service,
            uint serviceType,
            uint startType,
            uint errorControl,
            string binaryPathName,
            string loadOrderGroup,
            IntPtr tagId,
            string dependencies,
            string serviceStartName,
            string password,
            string displayName);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool QueryServiceConfig(
            IntPtr service,
            IntPtr serviceConfig,
            uint bufferSize,
            out uint bytesNeeded);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseServiceHandle(IntPtr serviceHandle);

        [StructLayout(LayoutKind.Sequential)]
        private struct QueryServiceConfigData
        {
            public uint ServiceType;
            public uint StartType;
            public uint ErrorControl;
            public IntPtr BinaryPathName;
            public IntPtr LoadOrderGroup;
            public uint TagId;
            public IntPtr Dependencies;
            public IntPtr ServiceStartName;
            public IntPtr DisplayName;
        }
    }
}
