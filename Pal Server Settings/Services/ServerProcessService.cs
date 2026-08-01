using System;
using System.Diagnostics;
using System.IO;

namespace PalWorldServerManager.Services
{
    public sealed class ServerProcessService : IDisposable
    {
        private Process? _serverProcess;
        private DateTime? _startedAt;
        private TimeSpan _previousProcessorTime;
        private DateTime _previousCpuCheckTime;
        private bool _ownsProcess;

        public event EventHandler<string>? OutputReceived;

        public event EventHandler<string>? ErrorReceived;

        public bool IsRunning
        {
            get
            {
                RefreshProcessState();

                return _serverProcess is not null &&
                       !_serverProcess.HasExited;
            }
        }

        public int? ProcessId
        {
            get
            {
                return IsRunning
                    ? _serverProcess?.Id
                    : null;
            }
        }

        public DateTime? StartedAt
        {
            get
            {
                return IsRunning
                    ? _startedAt
                    : null;
            }
        }

        public TimeSpan Uptime
        {
            get
            {
                if (!IsRunning ||
                    _startedAt is null)
                {
                    return TimeSpan.Zero;
                }

                return DateTime.Now -
                       _startedAt.Value;
            }
        }

        public long MemoryUsageBytes
        {
            get
            {
                if (!IsRunning ||
                    _serverProcess is null)
                {
                    return 0;
                }

                try
                {
                    _serverProcess.Refresh();

                    return _serverProcess.WorkingSet64;
                }
                catch
                {
                    return 0;
                }
            }
        }

        public double GetCpuUsagePercent()
        {
            if (!IsRunning ||
                _serverProcess is null)
            {
                ResetCpuTracking();

                return 0;
            }

            try
            {
                _serverProcess.Refresh();

                DateTime currentTime =
                    DateTime.UtcNow;

                TimeSpan currentProcessorTime =
                    _serverProcess.TotalProcessorTime;

                if (_previousCpuCheckTime ==
                    default)
                {
                    _previousCpuCheckTime =
                        currentTime;

                    _previousProcessorTime =
                        currentProcessorTime;

                    return 0;
                }

                double elapsedMilliseconds =
                    (currentTime -
                     _previousCpuCheckTime)
                    .TotalMilliseconds;

                double processorMilliseconds =
                    (currentProcessorTime -
                     _previousProcessorTime)
                    .TotalMilliseconds;

                _previousCpuCheckTime =
                    currentTime;

                _previousProcessorTime =
                    currentProcessorTime;

                if (elapsedMilliseconds <= 0)
                {
                    return 0;
                }

                double cpuUsage =
                    processorMilliseconds /
                    (elapsedMilliseconds *
                     Environment.ProcessorCount) *
                    100;

                return Math.Clamp(
                    cpuUsage,
                    0,
                    100);
            }
            catch
            {
                return 0;
            }
        }

        public void StartServer(
            string executablePath,
            string arguments = "")
        {
            if (IsRunning)
            {
                throw new InvalidOperationException(
                    "The Palworld server is already running.");
            }

            if (string.IsNullOrWhiteSpace(
                    executablePath))
            {
                throw new ArgumentException(
                    "Select the PalServer executable first.",
                    nameof(executablePath));
            }

            if (!File.Exists(
                    executablePath))
            {
                throw new FileNotFoundException(
                    "PalServer.exe could not be found.",
                    executablePath);
            }

            string workingDirectory =
                Path.GetDirectoryName(
                    executablePath)
                ?? throw new InvalidOperationException(
                    "The server working directory could not be determined.");

            ProcessStartInfo startInfo =
                new ProcessStartInfo
                {
                    FileName =
                        executablePath,

                    Arguments =
                        arguments ?? "",

                    WorkingDirectory =
                        workingDirectory,

                    UseShellExecute =
                        false,

                    CreateNoWindow =
                        true,

                    RedirectStandardOutput =
                        true,

                    RedirectStandardError =
                        true
                };

            Process process =
                new Process
                {
                    StartInfo =
                        startInfo,

                    EnableRaisingEvents =
                        true
                };

            process.Exited +=
                ServerProcess_Exited;

            process.OutputDataReceived +=
                ServerProcess_OutputDataReceived;

            process.ErrorDataReceived +=
                ServerProcess_ErrorDataReceived;

            if (!process.Start())
            {
                process.Dispose();

                throw new InvalidOperationException(
                    "Windows could not start PalServer.exe.");
            }

            _serverProcess =
                process;

            _ownsProcess =
                true;

            try
            {
                _startedAt =
                    process.StartTime;
            }
            catch
            {
                _startedAt =
                    DateTime.Now;
            }

            ResetCpuTracking();

            try
            {
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
            }
            catch
            {
                // Some executables may not expose redirected output.
                // Server monitoring still continues even if console
                // output is unavailable.
            }

            OutputReceived?.Invoke(
                this,
                $"PalServer started with PID {process.Id}.");
        }

        public void AttachToRunningServer(
            string processName =
                "PalServer-Win64-Test-Cmd")
        {
            if (IsRunning)
            {
                return;
            }

            string normalizedProcessName =
                Path.GetFileNameWithoutExtension(
                    processName);

            Process[] matchingProcesses =
                Process.GetProcessesByName(
                    normalizedProcessName);

            if (matchingProcesses.Length == 0)
            {
                return;
            }

            _serverProcess =
                matchingProcesses[0];

            _ownsProcess =
                false;

            _serverProcess.EnableRaisingEvents =
                true;

            _serverProcess.Exited +=
                ServerProcess_Exited;

            try
            {
                _startedAt =
                    _serverProcess.StartTime;
            }
            catch
            {
                _startedAt =
                    DateTime.Now;
            }

            ResetCpuTracking();

            for (int index = 1;
                 index < matchingProcesses.Length;
                 index++)
            {
                matchingProcesses[index].Dispose();
            }

            OutputReceived?.Invoke(
                this,
                "Attached to an already-running PalServer process. " +
                "Raw output cannot be captured retroactively for a process the manager did not start.");
        }

        public bool StopServer(
            TimeSpan gracefulWaitTime)
        {
            if (!IsRunning ||
                _serverProcess is null)
            {
                return true;
            }

            try
            {
                bool closeRequested =
                    _serverProcess.CloseMainWindow();

                if (closeRequested &&
                    _serverProcess.WaitForExit(
                        (int)gracefulWaitTime.TotalMilliseconds))
                {
                    ClearProcess();

                    return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        public void ForceStopServer()
        {
            if (!IsRunning ||
                _serverProcess is null)
            {
                return;
            }

            try
            {
                _serverProcess.Kill(
                    entireProcessTree: true);

                _serverProcess.WaitForExit(
                    5000);
            }
            finally
            {
                ClearProcess();
            }
        }

        public void RestartServer(
            string executablePath,
            string arguments,
            TimeSpan gracefulWaitTime)
        {
            if (IsRunning)
            {
                bool stoppedGracefully =
                    StopServer(
                        gracefulWaitTime);

                if (!stoppedGracefully)
                {
                    throw new InvalidOperationException(
                        "The server did not stop gracefully. " +
                        "Use Force Stop before restarting.");
                }
            }

            StartServer(
                executablePath,
                arguments);
        }

        private void ServerProcess_OutputDataReceived(
            object sender,
            DataReceivedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(
                    e.Data))
            {
                return;
            }

            OutputReceived?.Invoke(
                this,
                e.Data);
        }

        private void ServerProcess_ErrorDataReceived(
            object sender,
            DataReceivedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(
                    e.Data))
            {
                return;
            }

            ErrorReceived?.Invoke(
                this,
                e.Data);
        }

        private void ServerProcess_Exited(
            object? sender,
            EventArgs e)
        {
            OutputReceived?.Invoke(
                this,
                "PalServer process exited.");

            ClearProcess();
        }

        private void RefreshProcessState()
        {
            if (_serverProcess is null)
            {
                return;
            }

            try
            {
                if (_serverProcess.HasExited)
                {
                    ClearProcess();
                }
            }
            catch
            {
                ClearProcess();
            }
        }

        private void ResetCpuTracking()
        {
            _previousCpuCheckTime =
                default;

            _previousProcessorTime =
                TimeSpan.Zero;
        }

        private void ClearProcess()
        {
            Process? process =
                _serverProcess;

            _serverProcess =
                null;

            _startedAt =
                null;

            bool ownedProcess =
                _ownsProcess;

            _ownsProcess =
                false;

            ResetCpuTracking();

            if (process is null)
            {
                return;
            }

            try
            {
                process.Exited -=
                    ServerProcess_Exited;

                if (ownedProcess)
                {
                    process.OutputDataReceived -=
                        ServerProcess_OutputDataReceived;

                    process.ErrorDataReceived -=
                        ServerProcess_ErrorDataReceived;
                }

                process.Dispose();
            }
            catch
            {
                // Nothing else is required during cleanup.
            }
        }

        public void Dispose()
        {
            ClearProcess();
        }
    }
}