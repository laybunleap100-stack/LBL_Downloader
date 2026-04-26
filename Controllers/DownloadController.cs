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

            bool isWindows = System.Runtime.InteropServices.RuntimeInformation
                .IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows);

            
            string binDir = Path.Combine(_env.WebRootPath, "bin");
            _ytdl.YoutubeDLPath = isWindows
                ? Path.Combine(binDir, "yt-dlp.exe")
                : Path.Combine(binDir, "yt-dlp");

            _ytdl.FFmpegPath = isWindows
                ? Path.Combine(binDir, "ffmpeg.exe")
                : "/usr/bin/ffmpeg"; // installed via apt in Docker

     
            string downloadDir = Path.Combine(_env.WebRootPath, "downloads");
            Directory.CreateDirectory(downloadDir);
            _ytdl.OutputFolder = downloadDir;
        }

        public IActionResult Index() => View();


        [HttpPost]
        public async Task<IActionResult> StartDownload(string videoUrl)
        {
            if (string.IsNullOrEmpty(videoUrl))
                return Json(new { success = false, message = "សូមបញ្ចូល Link!" });

            try
            {
                var progress = new Progress<DownloadProgress>(p =>
                {
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

                if (res.Success && System.IO.File.Exists(res.Data))
                {
                    // Return just the filename so frontend can call GetFile
                    string fileName = Path.GetFileName(res.Data);
                    return Json(new { success = true, fileName = fileName });
                }

                return Json(new { success = false, message = string.Join(" ", res.ErrorOutput) });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

   
        [HttpGet]
        public IActionResult GetFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return BadRequest("File name is required.");

            // Sanitize: strip any path traversal
            fileName = Path.GetFileName(fileName);
            string filePath = Path.Combine(_env.WebRootPath, "downloads", fileName);

            if (!System.IO.File.Exists(filePath))
                return NotFound("File not found.");

            var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read,
                FileShare.Read, 4096, FileOptions.DeleteOnClose);

            return File(fileStream, "video/mp4", fileName);
        }
    }
}
