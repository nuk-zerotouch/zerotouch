using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Interactivity;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using ZeroTouch.UI.ViewModels;

namespace ZeroTouch.UI.Views
{
    public partial class DriverStateView : UserControl
    {
        private bool _dragging;
        private double _startX;
        
        private CancellationTokenSource? _soundCts;

        public DriverStateView()
        {
            InitializeComponent();
            
            this.PropertyChanged += (s, e) =>
            {
                if (e.Property == IsVisibleProperty)
                {
                    if (IsVisible)
                    {
                        StartWarningSound();
                    }
                    else
                    {
                        StopWarningSound();
                    }
                }
            };
        }
        
        private void StartWarningSound()
        {
            StopWarningSound();
            _soundCts = new CancellationTokenSource();
            _ = PlaySoundLoop(_soundCts.Token);
        }
        
        private void StopWarningSound()
        {
            _soundCts?.Cancel();
            _soundCts = null;
        }
        
        private async Task PlaySoundLoop(CancellationToken token)
        {
            string soundPath = System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, 
                "Assets", 
                "Music", 
                "alarm.mp3");

            if (!System.IO.File.Exists(soundPath))
            {
                Console.WriteLine($"[Error] Cannot find audio file. Path: {soundPath}");
                return;
            }
            
            while (!token.IsCancellationRequested)
            {
                try
                {
                    Process? process = null;
                    
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    {
                        // macOS uses afplay
                        process = Process.Start("afplay", soundPath);
                    }
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        // Windows uses PowerShell play (or System.Media.SoundPlayer)
                        
                        string psCommand = $"$wmp = New-Object -ComObject WMPlayer.OCX.7; " +
                                           $"$wmp.URL = '{soundPath}'; " +
                                           $"$wmp.Controls.play(); " +
                                           $"while ($wmp.playState -ne 1) {{ Start-Sleep -m 200 }}";

                        process = Process.Start(new ProcessStartInfo
                        {
                            FileName = "powershell",
                            Arguments = $"-c \"{psCommand}\"",
                            CreateNoWindow = true, // hide PowerShell window
                            UseShellExecute = false
                        });
                    }
                    
                    if (process != null)
                    {
                        try
                        {
                            // waiting for end or cancellation
                            await process.WaitForExitAsync(token);
                        }
                        catch (OperationCanceledException)
                        {
                            try { process.Kill(); } catch { /* Ignore kill process failure */ }
                            throw;
                        }
                    }
                    
                    // add delay before replay
                    await Task.Delay(200, token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error playing: {ex.Message}");
                    await Task.Delay(1000, token);
                }
            }
        }

        private void OnSliderPressed(object? sender, PointerPressedEventArgs e)
        {
            _dragging = true;
            _startX = e.GetPosition(this).X;
        }

        private void OnSliderMoved(object? sender, PointerEventArgs e)
        {
            if (!_dragging || DataContext is not DriverStateViewModel vm)
                return;

            var currentX = e.GetPosition(this).X;
            var delta = currentX - _startX;

            vm.SlideProgress = Math.Clamp(delta / 356.0, 0.0, 1.0);
        }

        private void OnSliderReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (DataContext is not DriverStateViewModel vm)
                return;

            _dragging = false;

            if (vm.SlideProgress >= 0.9)
            {
                vm.AcknowledgeFatigue();
            }
            else
            {
                vm.SlideProgress = 0.0;
            }
        }
    }
}
