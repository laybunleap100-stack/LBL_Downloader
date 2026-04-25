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

            string binFolder = Path.Combine(_env.WebRootPath, "bin");
            bool isWindows = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows);

            _ytdl.YoutubeDLPath = Path.Combine(binFolder, isWindows ? "yt-dlp.exe" : "yt-dlp");
            _ytdl.FFmpegPath = Path.Combine(binFolder, isWindows ? "ffmpeg.exe" : "ffmpeg");
            _ytdl.OutputFolder = Path.Combine(_env.WebRootPath, "downloads");
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> StartDownload(string videoUrl)
        {
            string downloadFolder = Path.Combine(_env.WebRootPath, "downloads");
            DirectoryInfo di = new DirectoryInfo(downloadFolder);
            if (di.Exists)
            {
                foreach (FileInfo file in di.GetFiles())
                {
                    if (file.CreationTime < DateTime.Now.AddMinutes(-30))
                    {
                        file.Delete();
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
            string decodedName = System.Net.WebUtility.UrlDecode(fileName);
            string filePath = Path.Combine(_env.WebRootPath, "downloads", decodedName);

            if (System.IO.File.Exists(filePath))
            {
                var fileBytes = System.IO.File.ReadAllBytes(filePath);
                System.IO.File.Delete(filePath);
                return File(fileBytes, "application/octet-stream", decodedName);
            }

            return NotFound("រកមិនឃើញវីដេអូ ឬវីដេអូត្រូវបានលុបចេញពី Server។");
        }
    }
}