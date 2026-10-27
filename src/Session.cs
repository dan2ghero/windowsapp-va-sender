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
   if(!synthetic) {
    if(config.Target==null)throw new InvalidOperationException(I18n.T(language,"Select an application before starting."));
    config.Target.Validate(language);
   }
   var targets=synthetic?new[]{Process.GetCurrentProcess()}:new[]{Process.GetProcessById(config.Target.ProcessId)};
   try {
    int pid=targets[0].Id;
    if(!synthetic)log(config.T("Video window: {0}; audio process: {1} (PID {2})",config.Target.Title,config.Target.Executable,pid));
    var command=config.Command(url,test,synthetic); string tools=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"tools");
    foreach(string file in test?new[]{"ffmpeg.exe","ffplay.exe","AppAudio.exe"}:new[]{"ffmpeg.exe","AppAudio.exe"}) if(!File.Exists(Path.Combine(tools,file))) throw new FileNotFoundException(I18n.T(language,"tools/{0} was not found.",file));
    if(test) player=Spawn(Path.Combine(tools,"ffplay.exe"),new[]{"-hide_banner","-loglevel",synthetic?"info":"warning","-probesize","1048576","-analyzeduration","1000000","-autoexit","-t","20","-i","pipe:0"},true,false);
    encoder=Spawn(Path.Combine(tools,"ffmpeg.exe"),command,true,test);
    audio=Spawn(Path.Combine(tools,"AppAudio.exe"),new[]{"--pid",pid.ToString()},false,true);
    Task toEncoder=Pump(audio.StandardOutput.BaseStream,encoder.StandardInput.BaseStream);
    Task toPlayer=test?Pump(encoder.StandardOutput.BaseStream,player.StandardInput.BaseStream):Task.CompletedTask;
    Completion=Monitor(toEncoder,toPlayer,test);
   } catch {Stop();throw;} finally {foreach(var p in targets) p.Dispose();}
  }
  Process Spawn(string exe,IEnumerable<string> args,bool input,bool output) {
   var p=new Process {StartInfo=new ProcessStartInfo(exe,Args.Join(args)) {UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=input,RedirectStandardOutput=output,RedirectStandardError=true,WorkingDirectory=AppDomain.CurrentDomain.BaseDirectory}};
   if(simulated&&Path.GetFileName(exe)=="ffplay.exe") {p.StartInfo.EnvironmentVariables["SDL_VIDEODRIVER"]="dummy";p.StartInfo.EnvironmentVariables["SDL_AUDIODRIVER"]="dummy";}
   p.ErrorDataReceived+=(s,e)=>{if(e.Data!=null) log(secret.Length>0?e.Data.Replace(secret,"[OUTPUT URL]"):e.Data);};
   p.Start();children.Add(p);p.BeginErrorReadLine();return p;
  }
  async Task Pump(Stream source,Stream destination) {
   try {await source.CopyToAsync(destination).ConfigureAwait(false);} catch(Exception e) {if(!stopping) log(I18n.T(language,"Pipe closed: {0}",e.Message));}
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
  public void Dispose() {Stop();foreach(var p in children) {try{p.Dispose();}catch{}}}
 }
}
