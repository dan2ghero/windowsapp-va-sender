using System;
using System.IO;
using System.Threading;
using System.Diagnostics;
using System.Runtime.InteropServices;




[ComImport, Guid("72A22D78-CDE4-431D-B8CC-843A71199B6D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IOperation { void GetActivateResult(out int result, [MarshalAs(UnmanagedType.IUnknown)] out object client); }
[Guid("41D949AB-9862-444A-80F6-C261334DA5EB"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface ICompletion { [PreserveSig] int ActivateCompleted(IOperationPublic operation); }
[ComImport, Guid("72A22D78-CDE4-431D-B8CC-843A71199B6D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IOperationPublic { void GetActivateResult(out int result, [MarshalAs(UnmanagedType.IUnknown)] out object client); }
[ComImport, Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IAudioClient {
 void Initialize(int mode, uint flags, long duration, long periodicity, ref Wave format, IntPtr session);
 void GetBufferSize(out uint frames); void GetStreamLatency(out long latency); void GetCurrentPadding(out uint padding);
 [PreserveSig] int IsFormatSupported(int mode, IntPtr format, out IntPtr closest);
 void GetMixFormat(out IntPtr format); void GetDevicePeriod(out long normal, out long minimum);
 void Start(); void Stop(); void Reset(); void SetEventHandle(IntPtr handle);
 void GetService(ref Guid iid, [MarshalAs(UnmanagedType.IUnknown)] out object service);
}
[ComImport, Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface ICapture {
 void GetBuffer(out IntPtr data, out uint frames, out uint flags, out ulong position, out ulong qpc);
 void ReleaseBuffer(uint frames); void GetNextPacketSize(out uint frames);
}
[StructLayout(LayoutKind.Sequential, Pack=2)]
struct Wave { public ushort tag, channels; public uint rate, bytes; public ushort align, bits, extra; }
[StructLayout(LayoutKind.Explicit, Size=24)]
struct Variant { [FieldOffset(0)] public ushort vt; [FieldOffset(8)] public uint size; [FieldOffset(16)] public IntPtr data; }
[ComVisible(true), ClassInterface(ClassInterfaceType.None)]
class Completion : ICompletion, IAgile {
 public readonly ManualResetEventSlim Ready = new(); public object? Client; public Exception? Error;
 public int ActivateCompleted(IOperationPublic op) {
  try { op.GetActivateResult(out int hr, out object value); Marshal.ThrowExceptionForHR(hr); Client=value; }
  catch(Exception e) { Error=e; } finally { Ready.Set(); } return 0;
 }
}
[Guid("94EA2B94-E9CC-49E0-C0FF-EE64CA8F5B90"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IAgile { }
class Program {
 [DllImport("Mmdevapi.dll", CharSet=CharSet.Unicode, PreserveSig=true)]
 static extern int ActivateAudioInterfaceAsync(string path, ref Guid iid, ref Variant args, ICompletion completion, out IOperation operation);
 static volatile bool stopped;
 static string stage="argument validation";
 [MTAThread] static int Main(string[] args) {
  if(args.Length==0 || args[0]=="--help") { Console.Error.WriteLine("GameAudio --process game.exe | --pid PID [--seconds N] [--output raw.pcm]\nOutput: raw signed 16-bit LE, 48000 Hz stereo. Diagnostics: stderr. Ctrl+C stops."); return 0; }
  try { return Run(args); } catch(IOException e) { Console.Error.WriteLine("Output closed: "+e.Message); return 4; }
  catch(Exception e) { Console.Error.WriteLine($"Audio failure at {stage}: HRESULT=0x{e.HResult:X8}; {e.Message}"); return 1; }
 }
 static int Run(string[] args) {
  string? name=null, output=null; int pid=0, seconds=0;
  for(int i=0;i<args.Length;i++) { if(i+1==args.Length) throw new ArgumentException("Missing option value"); string v=args[++i]; switch(args[i-1]) {
   case "--process": name=Path.GetFileNameWithoutExtension(v); break; case "--pid": pid=int.Parse(v); break;
   case "--seconds": seconds=int.Parse(v); if(seconds<=0) throw new ArgumentException("seconds must be positive"); break;
   case "--output": output=v; break; default: throw new ArgumentException("Unknown option");
  }}
  WindowsAppSender.Compatibility.EnsureSupported();
  Console.Error.WriteLine(WindowsAppSender.Compatibility.Description);
  if(pid!=0 && name!=null) throw new ArgumentException("Choose process OR pid");
  if(pid==0) { if(name==null) throw new ArgumentException("Specify --process or --pid"); var ps=Process.GetProcessesByName(name); if(ps.Length!=1) throw new ArgumentException($"Expected one {name}, found {ps.Length}. Start game or use --pid."); pid=ps[0].Id; foreach(var p in ps) p.Dispose(); }
  using var target=Process.GetProcessById(pid);
  using var identity=new WindowsAppSender.ProcessIdentity(pid);
  Console.CancelKeyPress+=(s,e)=>{ e.Cancel=true; stopped=true; };
  IntPtr data=Marshal.AllocHGlobal(12); IAudioClient? client=null; object? capObject=null; IOperation? operation=null;
  var cb=new Completion();
  try {
   // AUDIOCLIENT_ACTIVATION_PARAMS: PROCESS_LOOPBACK=1, PID, INCLUDE_TARGET_PROCESS_TREE=0.
   Marshal.WriteInt32(data,0,1); Marshal.WriteInt32(data,4,pid); Marshal.WriteInt32(data,8,0);
   var pv=new Variant { vt=65, size=12, data=data }; var iid=typeof(IAudioClient).GUID;
   stage="ActivateAudioInterfaceAsync";
   Console.Error.WriteLine("Process Loopback: requesting activation...");
   Marshal.ThrowExceptionForHR(ActivateAudioInterfaceAsync("VAD\\Process_Loopback",ref iid,ref pv,cb,out operation));
   stage="activation completion (10-second timeout)";
   if(!cb.Ready.Wait(10000)) throw new TimeoutException("Process Loopback activation did not complete within 10 seconds.");
   stage="activation completion result";
   if(cb.Error!=null) throw cb.Error;
   Console.Error.WriteLine("Process Loopback: activation completed.");
   client=(IAudioClient)cb.Client!;
   var wave=new Wave { tag=1,channels=2,rate=48000,bytes=192000,align=4,bits=16 };
   using var ready=new AutoResetEvent(false);
   stage="IAudioClient.Initialize (s16le 48000 stereo)";
   client.Initialize(0,0x80000000u|0x00020000u|0x00040000u,0,0,ref wave,IntPtr.Zero);
   client.SetEventHandle(ready.SafeWaitHandle.DangerousGetHandle());
   var captureId=typeof(ICapture).GUID; client.GetService(ref captureId,out capObject); var capture=(ICapture)capObject;
   using Stream sink=output==null?Console.OpenStandardOutput():new FileStream(output,FileMode.CreateNew,FileAccess.Write,FileShare.Read);
   Console.Error.WriteLine($"Capturing PID {pid} ({target.ProcessName}) and descendants; s16le 48000 stereo.");
   stage="IAudioClient.Start";
   client.Start(); stage="capture packets / PCM output"; var clock=Stopwatch.StartNew(); long total=0; int discontinuities=0;
   while(!stopped && identity.IsRunning && (seconds==0 || clock.Elapsed.TotalSeconds<seconds)) {
    ready.WaitOne(100); capture.GetNextPacketSize(out uint next);
    while(next!=0) {
     capture.GetBuffer(out IntPtr ptr,out uint frames,out uint flags,out _,out _);
     byte[] buffer=new byte[checked((int)frames*4)];
     try { if((flags&2)==0) Marshal.Copy(ptr,buffer,0,buffer.Length); } finally { capture.ReleaseBuffer(frames); }
     if((flags&1)!=0) discontinuities++;
     sink.Write(buffer,0,buffer.Length); total+=frames; capture.GetNextPacketSize(out next);
    }
   }
   client.Stop(); sink.Flush(); Console.Error.WriteLine($"Stopped: {total} frames, {total/48000.0:F3}s PCM, {discontinuities} discontinuities."); return 0;
  } finally {
   if(client!=null) { try { client.Stop(); } catch { } }
   if(capObject!=null) Marshal.ReleaseComObject(capObject);
   if(client!=null) Marshal.ReleaseComObject(client);
   if(operation!=null) Marshal.ReleaseComObject(operation);
   Marshal.FreeHGlobal(data); GC.KeepAlive(cb);
  }
 }
}

