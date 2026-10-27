using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace WindowsAppSender {
 static class Bootstrap {
  const string Release="https://github.com/BtbN/FFmpeg-Builds/releases/download/autobuild-2026-05-31-13-22/ffmpeg-N-124714-g49a77d37be-win64-gpl.zip";
  const string Sha256="157e26f92fc48ceead6204ef838342be926ee9d10b93f38ccbe9bbdf8a90e4b9";
  public static bool Ready {get {string root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"tools");return File.Exists(Path.Combine(root,"ffmpeg.exe"))&&File.Exists(Path.Combine(root,"ffplay.exe"));}}
  public static async Task Ensure(Action<string> progress,CancellationToken token,string language="ENG") {
   if(Ready)return;
   string root=AppDomain.CurrentDomain.BaseDirectory,tools=Path.Combine(root,"tools"),zip=Path.Combine(root,"ffmpeg-download.partial");
   Directory.CreateDirectory(tools);
   progress(I18n.T(language,"First-run setup: downloading official FFmpeg (about 211 MB)…"));
   ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;
   try {
    using(var web=new WebClient())using(token.Register(()=>web.CancelAsync())) {
     web.Headers[HttpRequestHeader.UserAgent]="windowsapp-va-sender/1.0";
     web.DownloadProgressChanged+=(s,e)=>progress(I18n.T(language,"First-run setup: downloading FFmpeg {0}%",e.ProgressPercentage));
     await web.DownloadFileTaskAsync(new Uri(Release),zip);
    }
    token.ThrowIfCancellationRequested();progress(I18n.T(language,"First-run setup: verifying SHA-256 and extracting…"));
    await Task.Run(()=>{
     using(var sha=SHA256.Create())using(var stream=File.OpenRead(zip))if(BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant()!=Sha256)throw new InvalidDataException(I18n.T(language,"The official FFmpeg ZIP SHA-256 does not match. Installation was cancelled."));
     using(var archive=ZipFile.OpenRead(zip)) {
      foreach(string file in new[]{"ffmpeg.exe","ffplay.exe"}) {
       token.ThrowIfCancellationRequested();var entry=archive.Entries.Single(e=>e.FullName.EndsWith("/bin/"+file,StringComparison.OrdinalIgnoreCase));
       string part=Path.Combine(tools,file+".partial");entry.ExtractToFile(part,true);
      }
      Directory.CreateDirectory(Path.Combine(root,"licenses"));
      var license=archive.Entries.FirstOrDefault(e=>e.Name=="LICENSE.txt");if(license!=null)license.ExtractToFile(Path.Combine(root,"licenses","FFmpeg-GPL.txt"),true);
      foreach(string file in new[]{"ffmpeg.exe","ffplay.exe"}) {string dest=Path.Combine(tools,file);if(File.Exists(dest))File.Delete(dest);File.Move(dest+".partial",dest);}
     }
    },token);
    progress(I18n.T(language,"FFmpeg is ready."));
   } finally {try{if(File.Exists(zip))File.Delete(zip);}catch{}}
  }
 }
}
