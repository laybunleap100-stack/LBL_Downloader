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

            // កំណត់ Path សម្រាប់ Linux/Render ឱ្យបានត្រឹមត្រូវ
            bool isWindows = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows);
            _ytdl.YoutubeDLPath = isWindows ? Path.Combine(AppContext.BaseDirectory, "bin", "yt-dlp.exe") : "/usr/local/bin/yt-dlp";
            _ytdl.FFmpegPath = isWindows ? Path.Combine(AppContext.BaseDirectory, "bin", "ffmpeg.exe") : "/usr/bin/ffmpeg";
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> StartDownload(string videoUrl)
        {
            if (string.IsNullOrEmpty(videoUrl))
            {
                return Json(new { success = false, message = "សូមបញ្ចូល Link វីដេអូ!" });
            }

            try 
            {
                var progress = new Progress<DownloadProgress>(p => {
                    _hubContext.Clients.All.SendAsync("ReceiveProgress", Math.Round(p.Progress * 100));
                });

                var options = new OptionSet()
                {
                    NoCheckCertificates = true,
                    // បង្ខំយក MP4 ជានិច្ច
                    Format = "bestvideo[ext=mp4]+bestaudio[ext=m4a]/best[ext=mp4]/best"
                };

                // ប្រើ RunVideoDownload ដើម្បីទាញយកទិន្នន័យ (Data) មកទុកក្នុង Memory
                var res = await _ytdl.RunVideoDownload(videoUrl, progress: progress, overrideOptions: options);
                
                if (res.Success)
                {
                    // ទាញយក File Path បណ្ដោះអាសន្នដែល yt-dlp បានបង្កើត
                    string tempFilePath = res.Data; 
                    string finalFileName = $"video_{DateTime.Now.Ticks}.mp4";

                    if (System.IO.File.Exists(tempFilePath))
                    {
                        var fileBytes = await System.IO.File.ReadAllBytesAsync(tempFilePath);
                        
                        // លុប File ចោលភ្លាមៗក្រោយអានរួច ដើម្បីកុំឱ្យពេញទំហំផ្ទុកលើ Render
                        try { System.IO.File.Delete(tempFilePath); } catch { }

                        // បញ្ជូន File ទៅកាន់ Browser ជាមួយ MIME Type ត្រឹមត្រូវ
                        return File(fileBytes, "video/mp4", finalFileName);
                    }
                }
                
                return Json(new { success = false, message = "កំហុស៖ " + string.Join(" ", res.ErrorOutput) });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // មុខងារនេះអាចទុកសម្រាប់ករណីប្រើប្រាស់ផ្សេងៗ
        [HttpGet]
        public IActionResult GetFileAndPath(string fileName)
        {
            return NotFound("មុខងារនេះត្រូវបានជំនួសដោយការទាញយកផ្ទាល់ (Direct Download)។");
        }
    }
}