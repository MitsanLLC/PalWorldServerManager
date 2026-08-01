using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PalWorldServerManager.Services
{
    public sealed class SteamCmdService : IDisposable
    {
        private Process? _process;

        public event EventHandler<string>? OutputReceived;
        public event EventHandler<string>? ErrorReceived;

        public async Task UpdatePalworldServerAsync(
            string steamCmdPath,
            string serverInstallDirectory,
            CancellationToken cancellationToken = default)
        {
            if (!File.Exists(steamCmdPath))
            {
                throw new FileNotFoundException("steamcmd.exe could not be found.", steamCmdPath);
            }

            if (!Directory.Exists(serverInstallDirectory))
            {
                throw new DirectoryNotFoundException(
                    $"The PalServer installation directory could not be found:\n{serverInstallDirectory}");
            }

            string arguments =
                $"+force_install_dir \"{serverInstallDirectory}\" " +
                "+login anonymous " +
                "+app_update 2394010 validate " +
                "+quit";

            Process process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = steamCmdPath,
                    Arguments = arguments,
                    WorkingDirectory = Path.GetDirectoryName(steamCmdPath) ?? "",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                },
                EnableRaisingEvents = true
            };

            process.OutputDataReceived += Process_OutputDataReceived;
            process.ErrorDataReceived += Process_ErrorDataReceived;
            _process = process;

            try
            {
                if (!process.Start())
                {
                    throw new InvalidOperationException("Windows could not start SteamCMD.");
                }

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await process.WaitForExitAsync(cancellationToken);

                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException(
                        $"SteamCMD exited with code {process.ExitCode}.");
                }
            }
            finally
            {
                process.OutputDataReceived -= Process_OutputDataReceived;
                process.ErrorDataReceived -= Process_ErrorDataReceived;
                process.Dispose();
                _process = null;
            }
        }

        private void Process_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                OutputReceived?.Invoke(this, e.Data);
            }
        }

        private void Process_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                ErrorReceived?.Invoke(this, e.Data);
            }
        }

        public void Dispose()
        {
            try
            {
                if (_process is not null && !_process.HasExited)
                {
                    _process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
            }

            _process?.Dispose();
            _process = null;
        }
    }
}