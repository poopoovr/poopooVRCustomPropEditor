using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace poopooVRCustomPropEditor.Features
{
    public class MediaInfo
    {
        public string Title { get; set; } = "";
        public string Artist { get; set; } = "";
        public string Description { get; set; } = "";
        public string Status { get; set; } = "Unknown";
        public double ElapsedTime { get; set; }
        public double StartTime { get; set; }
        public double EndTime { get; set; }
        public string ThumbnailBase64 { get; set; } = "";

        public bool IsPlaying => Status == "Playing";
        public bool IsPaused => Status == "Paused";
        public bool HasMedia => !string.IsNullOrEmpty(Title);

        public string FormattedTime
        {
            get
            {
                if (EndTime <= 0) return "--:--";
                var elapsed = TimeSpan.FromSeconds(ElapsedTime);
                var total = TimeSpan.FromSeconds(EndTime);
                return $"{elapsed:mm\\:ss} / {total:mm\\:ss}";
            }
        }
    }

    public class MediaController : MonoBehaviour
    {
        #region Windows API

        private const byte VK_MEDIA_NEXT_TRACK = 0xB0;
        private const byte VK_MEDIA_PREV_TRACK = 0xB1;
        private const byte VK_MEDIA_STOP = 0xB2;
        private const byte VK_MEDIA_PLAY_PAUSE = 0xB3;
        private const byte VK_VOLUME_MUTE = 0xAD;
        private const byte VK_VOLUME_DOWN = 0xAE;
        private const byte VK_VOLUME_UP = 0xAF;

        private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        #endregion

        #region Public Properties

        public bool IsEnabled { get; set; } = true;
        public bool IsPlaying => CurrentMedia?.IsPlaying ?? false;
        public MediaInfo CurrentMedia { get; private set; } = new MediaInfo();
        public string QuickSongPath { get; set; } = "";
        public bool IsQuickSongAvailable => !string.IsNullOrEmpty(QuickSongPath) && File.Exists(QuickSongPath);
        public float RefreshInterval { get; set; } = 2f;

        #endregion

        #region Private Fields

        private float lastRefreshTime;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            Instance = this;
            ExtractQuickSong();
        }

        private void Update()
        {
            if (IsQuickSongAvailable && Time.time - lastRefreshTime >= RefreshInterval)
            {
                RefreshMediaInfo();
                lastRefreshTime = Time.time;
            }

            if (!CurrentMedia.IsPaused && CurrentMedia.HasMedia)
            {
                CurrentMedia.ElapsedTime += Time.deltaTime;
            }
        }

        #endregion

        #region QuickSong Integration

        public static MediaController Instance { get; private set; }

        private void ExtractQuickSong()
        {
            string[] searchPaths = new[]
            {
                Path.Combine(Application.dataPath, "..", "BepInEx", "plugins", "QuickSong.exe"),
                Path.Combine(Application.dataPath, "..", "QuickSong.exe"),
                Path.Combine(Application.dataPath, "..", "Mods", "QuickSong.exe"),
            };

            foreach (var path in searchPaths)
            {
                string fullPath = Path.GetFullPath(path);
                if (File.Exists(fullPath))
                {
                    QuickSongPath = fullPath;
                    Debug.Log($"[MediaController] Found QuickSong at: {fullPath}");
                    return;
                }
            }

            try
            {
                string resourceName = "poopooVRCustomPropEditor.Resources.QuickSong.exe";
                QuickSongPath = Path.Combine(Path.GetTempPath(), "QuickSong.exe");

                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                using (Stream resourceStream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (resourceStream != null)
                    {
                        if (File.Exists(QuickSongPath))
                            File.Delete(QuickSongPath);

                        using (FileStream fs = new FileStream(QuickSongPath, FileMode.Create, FileAccess.Write))
                        {
                            resourceStream.CopyTo(fs);
                        }

                        Debug.Log($"[MediaController] Extracted QuickSong to: {QuickSongPath}");
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MediaController] Failed to extract QuickSong: {ex.Message}");
            }

            string tempPath = Path.Combine(Path.GetTempPath(), "QuickSong.exe");
            if (File.Exists(tempPath))
            {
                QuickSongPath = tempPath;
                Debug.Log($"[MediaController] Found QuickSong in temp: {tempPath}");
                return;
            }

            Debug.Log("[MediaController] QuickSong.exe not found. Place it in BepInEx/plugins folder for media info.");
            QuickSongPath = "";
        }

        public void RefreshMediaInfo()
        {
            if (!IsQuickSongAvailable) return;

            try
            {
                string jsonOutput = RunQuickSong("-all");

                if (!string.IsNullOrEmpty(jsonOutput) && jsonOutput.StartsWith("{"))
                {
                    ParseMediaJson(jsonOutput);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MediaController] Error refreshing media info: {ex.Message}");
            }
        }

        public string GetCurrentSongDescription()
        {
            if (!IsQuickSongAvailable)
                return "QuickSong not found";

            try
            {
                return RunQuickSong("-description");
            }
            catch
            {
                return "Error getting song info";
            }
        }

        public string GetPlaybackStatus()
        {
            if (!IsQuickSongAvailable)
                return "Unknown";

            try
            {
                return RunQuickSong("-status");
            }
            catch
            {
                return "Error";
            }
        }

        private string RunQuickSong(string argument)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = QuickSongPath,
                    Arguments = argument,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using (Process process = Process.Start(psi))
                {
                    string output = process.StandardOutput.ReadToEnd().Trim();
                    process.WaitForExit(1000);
                    return output;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MediaController] QuickSong error: {ex.Message}");
                return "";
            }
        }

        private void ParseMediaJson(string json)
        {
            try
            {
                CurrentMedia.Title = ExtractJsonString(json, "Title");
                CurrentMedia.Artist = ExtractJsonString(json, "Artist");
                CurrentMedia.Description = ExtractJsonString(json, "Description");
                CurrentMedia.Status = ExtractJsonString(json, "Status");
                CurrentMedia.ThumbnailBase64 = ExtractJsonString(json, "ThumbnailBase64");

                if (double.TryParse(ExtractJsonValue(json, "ElapsedTime"), out double elapsed))
                    CurrentMedia.ElapsedTime = elapsed;
                if (double.TryParse(ExtractJsonValue(json, "StartTime"), out double start))
                    CurrentMedia.StartTime = start;
                if (double.TryParse(ExtractJsonValue(json, "EndTime"), out double end))
                    CurrentMedia.EndTime = end;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MediaController] Error parsing media JSON: {ex.Message}");
            }
        }

        private string ExtractJsonString(string json, string key)
        {
            var match = Regex.Match(json, $"\"{key}\"\\s*:\\s*\"([^\"]*)\"");
            return match.Success ? match.Groups[1].Value : "";
        }

        private string ExtractJsonValue(string json, string key)
        {
            var match = Regex.Match(json, $"\"{key}\"\\s*:\\s*([\\d.]+)");
            return match.Success ? match.Groups[1].Value : "0";
        }

        #endregion

        #region Public Methods

        public void PlayPause()
        {
            if (!IsEnabled) return;

            try
            {
                SendMediaKey(VK_MEDIA_PLAY_PAUSE);
                Debug.Log("[MediaController] Play/Pause toggled");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MediaController] PlayPause error: {ex.Message}");
            }
        }

        public void NextTrack()
        {
            if (!IsEnabled) return;

            try
            {
                SendMediaKey(VK_MEDIA_NEXT_TRACK);
                Debug.Log("[MediaController] Next track");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MediaController] NextTrack error: {ex.Message}");
            }
        }

        public void PreviousTrack()
        {
            if (!IsEnabled) return;

            try
            {
                SendMediaKey(VK_MEDIA_PREV_TRACK);
                Debug.Log("[MediaController] Previous track");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MediaController] PreviousTrack error: {ex.Message}");
            }
        }

        public void Stop()
        {
            if (!IsEnabled) return;

            try
            {
                SendMediaKey(VK_MEDIA_STOP);
                Debug.Log("[MediaController] Stop");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MediaController] Stop error: {ex.Message}");
            }
        }

        public void VolumeUp()
        {
            if (!IsEnabled) return;

            try
            {
                SendMediaKey(VK_VOLUME_UP);
                Debug.Log("[MediaController] Volume up");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MediaController] VolumeUp error: {ex.Message}");
            }
        }

        public void VolumeDown()
        {
            if (!IsEnabled) return;

            try
            {
                SendMediaKey(VK_VOLUME_DOWN);
                Debug.Log("[MediaController] Volume down");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MediaController] VolumeDown error: {ex.Message}");
            }
        }

        public void ToggleMute()
        {
            if (!IsEnabled) return;

            try
            {
                SendMediaKey(VK_VOLUME_MUTE);
                Debug.Log("[MediaController] Mute toggled");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MediaController] ToggleMute error: {ex.Message}");
            }
        }

        #endregion

        #region Private Methods

        private void SendMediaKey(byte keyCode)
        {
            keybd_event(keyCode, 0, KEYEVENTF_EXTENDEDKEY, UIntPtr.Zero);
            keybd_event(keyCode, 0, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        #endregion
    }
}
