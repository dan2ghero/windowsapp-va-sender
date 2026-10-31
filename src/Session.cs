using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace WindowsAppSender {
 sealed class Session : IDisposable {
  readonly List<Process> children=new List<Process>();
  readonly Action<string> log;
  string epochDirectory,epochFile;
  Process audio,encoder,player;
  volatile bool stopping;
  bool simulated;
  public bool Successful {get;private set;}
  public int[] OwnedProcessIds {get {return children.Select(p=>p.Id).ToArray();}}
  public Task Completion {get;private set;}
  string secret="";
  string language="ENG";
  public Session(Action<string> logger) {log=logger;}
  public void Start(Config config,string url,bool test,bool synthetic=false) {
   language=config.Language;simulated=synthetic;secret=url;
   epochDirectory=Path.Combine(Path.GetTempPath(),"va-sender-sync-"+Guid.NewGuid().ToString("N"));
   Directory.CreateDirectory(epochDirectory);epochFile=Path.Combine(epochDirectory,"epoch.txt");
   log("AUTO SYNC: aligning video to the audio acquisition clock; manual audio correction remains additional.");

   if(!synthetic) {
    if(config.Target==null)throw new InvalidOperationException(I18n.T(language,"Select an application before starting."));
    config.Target.Validate(language);
   }
   var targets=synthetic?new[]{Process.GetCurrentProcess()}:new[]{Process.GetProcessById(config.Target.ProcessId)};
   try {
    int pid=targets[0].Id;
    if(!synthetic)log(config.T("Video window: {0}; audio process: {1} (PID {2})",config.Target.Title,config.Target.Executable,pid));
    var command=config.Command(url,test,synthetic);
    string tools=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"tools");
    foreach(string file in test?new[]{"ffmpeg.exe","ffplay.exe","AppAudio.exe"}:new[]{"ffmpeg.exe","AppAudio.exe"}) if(!File.Exists(Path.Combine(tools,file))) throw new FileNotFoundException(I18n.T(language,"tools/{0} was not found.",file));
    if(test) player=Spawn(Path.Combine(tools,"ffplay.exe"),new[]{"-hide_banner","-loglevel",synthetic?"info":"warning","-probesize","1048576","-analyzeduration","1000000","-autoexit","-t","20","-i","pipe:0"},true,false);
    string native=Path.Combine(tools,"clock","ffmpeg.exe");
    if(!File.Exists(native))throw new FileNotFoundException("Missing synchronization engine. Extract the entire ZIP.",native);
    NativeBuild.Validate(native);
    encoder=Spawn(native,command,true,test);
    audio=Spawn(Path.Combine(tools,"AppAudio.exe"),new[]{"--pid",pid.ToString(),"--epoch",epochFile},false,true);
    Task toEncoder=Pump(audio.StandardOutput.BaseStream,encoder.StandardInput.BaseStream,true);
    Task toPlayer=test?Pump(encoder.StandardOutput.BaseStream,player.StandardInput.BaseStream):Task.CompletedTask;
    Completion=Monitor(toEncoder,toPlayer,test);
   } catch {Stop();throw;} finally {foreach(var p in targets) p.Dispose();}
  }
  Process Spawn(string exe,IEnumerable<string> args,bool input,bool output) {
   var p=new Process {StartInfo=new ProcessStartInfo(exe,Args.Join(args)) {UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=input,RedirectStandardOutput=output,RedirectStandardError=true,WorkingDirectory=AppDomain.CurrentDomain.BaseDirectory}};
   if(simulated&&Path.GetFileName(exe)=="ffplay.exe") {p.StartInfo.EnvironmentVariables["SDL_VIDEODRIVER"]="dummy";p.StartInfo.EnvironmentVariables["SDL_AUDIODRIVER"]="dummy";}
   p.ErrorDataReceived+=(s,e)=>{if(e.Data!=null) {
    string name=Path.GetFileName(exe);
    if(name!="ffmpeg.exe"||e.Data.IndexOf("error",StringComparison.OrdinalIgnoreCase)>=0||e.Data.IndexOf("warning",StringComparison.OrdinalIgnoreCase)>=0)log(secret.Length>0?e.Data.Replace(secret,"[OUTPUT URL]"):e.Data);
   }};
   if(Path.GetFileName(exe)=="ffmpeg.exe" && !simulated)p.StartInfo.EnvironmentVariables["SENDER_AUDIO_EPOCH_FILE"]=epochFile;
   p.Start();children.Add(p);p.BeginErrorReadLine();return p;
  }
  async Task Pump(Stream source,Stream destination,bool pcm=false) {
   try {
    byte[] buffer=new byte[81920];
    while(true){int count=await source.ReadAsync(buffer,0,buffer.Length).ConfigureAwait(false);if(count==0)break;
     await destination.WriteAsync(buffer,0,count).ConfigureAwait(false);
    }
   } catch(Exception e) {if(!stopping) log(I18n.T(language,"Pipe closed: {0}",e.Message));}
   finally {try {destination.Close();} catch {}}
  }
  async Task Monitor(Task toEncoder,Task toPlayer,bool test) {
   try {
    // Observe each child, including preview-window closure and early audio failure.
    while(!stopping && !encoder.HasExited) {
     if(audio.HasExited || (test&&player.HasExited)) {Stop();break;}
     await Task.Delay(100).ConfigureAwait(false);
    }
    await Task.Run(()=>encoder.WaitForExit()).ConfigureAwait(false);
    if(!stopping && encoder.ExitCode!=0) log(I18n.T(language,"FFmpeg exited with an error ({0}). Check the log.",encoder.ExitCode));
    else if(!stopping) {
     if(test&&player!=null)await Task.Run(()=>player.WaitForExit(3000)).ConfigureAwait(false);
     Successful=!test||(player.HasExited&&player.ExitCode==0);
     log(I18n.T(language,test?"20-second TEST completed.":"Sending finished."));
    }
   } finally {Stop(); await Task.WhenAll(toEncoder,toPlayer).ConfigureAwait(false);}
  }
  public void Stop() {stopping=true; foreach(var p in children.ToArray().Reverse()) {try {if(!p.HasExited) p.Kill();}catch{}}}
  public void Dispose() {Stop();foreach(var p in children) {try{p.Dispose();}catch{}}try {if(epochDirectory!=null&&Directory.Exists(epochDirectory)) {foreach(string f in new[]{epochFile,epochFile+".partial"})if(File.Exists(f))File.Delete(f);Directory.Delete(epochDirectory);}}catch{} }

 }
}
