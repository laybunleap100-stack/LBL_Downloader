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

            string webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
            string binFolder = Path.Combine(webRoot, "bin");
            string downloadFolder = Path.Combine(webRoot, "downloads");

            if (!Directory.Exists(binFolder)) Directory.CreateDirectory(binFolder);
            if (!Directory.Exists(downloadFolder)) Directory.CreateDirectory(downloadFolder);

            bool isWindows = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows);

            _ytdl.YoutubeDLPath = isWindows ? Path.Combine(binFolder, "yt-dlp.exe") : "/usr/local/bin/yt-dlp";
            _ytdl.FFmpegPath = isWindows ? Path.Combine(binFolder, "ffmpeg.exe") : "/usr/bin/ffmpeg";
            _ytdl.OutputFolder = downloadFolder;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> StartDownload(string videoUrl)
        {
            string webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
            string downloadFolder = Path.Combine(webRoot, "downloads");

            if (Directory.Exists(downloadFolder))
            {
                DirectoryInfo di = new DirectoryInfo(downloadFolder);
                foreach (FileInfo file in di.GetFiles())
                {
                    if (file.CreationTime < DateTime.Now.AddMinutes(-30))
                    {
                        try { file.Delete(); } catch { }
                    }
                }
            }

            if (string.IsNullOrEmpty(videoUrl))
            {
                return Json(new { success = false, message = "សូមបញ្ចូល Link វីដេអូ!" });
            }

            try 
            {
                var progress = new Progress<DownloadProgress>(p => {
                    _hubContext.Clients.All.SendAsync("ReceiveProgress", Math.Round(p.Progress * 100));
                });

                string safeFileName = "video_" + DateTime.Now.Ticks;
                var options = new OptionSet()
                {
                    Output = Path.Combine(_ytdl.OutputFolder, safeFileName + ".%(ext)s")
                };

                var res = await _ytdl.RunVideoDownload(videoUrl, progress: progress, overrideOptions: options);
                
                if (res.Success)
                {
                    string actualFileName = Path.GetFileName(res.Data);
                    return Json(new { success = true, fileName = actualFileName });
                }
                return Json(new { success = false, message = "កំហុស៖ " + string.Join(" ", res.ErrorOutput) });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public IActionResult GetFileAndPath(string fileName)
        {
            string webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
            string decodedName = System.Net.WebUtility.UrlDecode(fileName);
            string filePath = Path.Combine(webRoot, "downloads", decodedName);

            if (System.IO.File.Exists(filePath))
            {
                var fileBytes = System.IO.File.ReadAllBytes(filePath);
                try { System.IO.File.Delete(filePath); } catch { }
                return File(fileBytes, "application/octet-stream", decodedName);
            }

            return NotFound("រកមិនឃើញវីដេអូ ឬវីដេអូត្រូវបានលុបចេញពី Server។");
        }
    }
}