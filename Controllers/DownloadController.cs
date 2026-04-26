using Microsoft.AspNetCore.Mvc;
using YoutubeDLSharp;
using YoutubeDLSharp.Options;
using System.IO;
using Microsoft.AspNetCore.SignalR;
using LBL_Downloader;

namespace LBL_Downloader.Controllers
{
    public class DownloadController : Controller
    {
        private readonly YoutubeDL _ytdl;
        private readonly IHubContext<DownloadHub> _hubContext;
        private readonly IWebHostEnvironment _env;

        public DownloadController(IHubContext<DownloadHub> hubContext, IWebHostEnvironment env)
        {
            _hubContext = hubContext;
            _env = env;
            _ytdl = new YoutubeDL();

            bool isWindows = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows);
            _ytdl.YoutubeDLPath = isWindows ? Path.Combine(AppContext.BaseDirectory, "bin", "yt-dlp.exe") : "/usr/local/bin/yt-dlp";
            _ytdl.FFmpegPath = isWindows ? Path.Combine(AppContext.BaseDirectory, "bin", "ffmpeg.exe") : "/usr/bin/ffmpeg";
        }

        public IActionResult Index() => View();

        [HttpPost]
        public async Task<IActionResult> StartDownload(string videoUrl)
        {
            if (string.IsNullOrEmpty(videoUrl)) return Json(new { success = false, message = "សូមបញ្ចូល Link វីដេអូ!" });

            try 
            {
                var progress = new Progress<DownloadProgress>(p => {
                    _hubContext.Clients.All.SendAsync("ReceiveProgress", Math.Round(p.Progress * 100));
                });

                var options = new OptionSet()
                {
                    NoCheckCertificates = true,
                    Format = "best[ext=mp4]/best", 
                    UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/123.0.0.0 Safari/537.36",
                    NoPart = true,
                    JsRuntimes = "node",
                    Impersonate = "chrome"
                };

                var res = await _ytdl.RunVideoDownload(videoUrl, progress: progress, overrideOptions: options);
                
                if (res.Success)
                {
                    string tempFilePath = res.Data; 
                    if (System.IO.File.Exists(tempFilePath))
                    {
                        var fileStream = new FileStream(tempFilePath, FileMode.Open, FileAccess.Read, FileShare.None, 4096, FileOptions.DeleteOnClose);
                        return File(fileStream, "video/mp4", $"video_{DateTime.Now.Ticks}.mp4");
                    }
                }
                
                return Json(new { success = false, message = "កំហុស៖ " + string.Join(" ", res.ErrorOutput) });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
    }
}