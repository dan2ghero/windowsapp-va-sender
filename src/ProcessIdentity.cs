using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
namespace WindowsAppSender {
 // Query-only access; no VM read/write, synchronization, or injection rights.
 sealed class ProcessIdentity:IDisposable {
  readonly SafeFileHandle handle;
  public ProcessIdentity(int pid) {
   handle=OpenProcess(0x1000,false,pid); // PROCESS_QUERY_LIMITED_INFORMATION
   if(handle.IsInvalid){int error=Marshal.GetLastWin32Error();handle.Dispose();throw new Win32Exception(error);}
  }
  public long StartTicks {get{long created,exited,kernel,user;ReadTimes(out created,out exited,out kernel,out user);return DateTime.FromFileTimeUtc(created).Ticks;}}
  public bool IsRunning {get{long created,exited,kernel,user;ReadTimes(out created,out exited,out kernel,out user);return exited==0;}}
  void ReadTimes(out long created,out long exited,out long kernel,out long user){if(!GetProcessTimes(handle,out created,out exited,out kernel,out user))throw new Win32Exception(Marshal.GetLastWin32Error());}
  public void Dispose(){handle.Dispose();}
  [DllImport("kernel32.dll",SetLastError=true)]static extern SafeFileHandle OpenProcess(uint access,bool inherit,int pid);
  [DllImport("kernel32.dll",SetLastError=true)]static extern bool GetProcessTimes(SafeFileHandle process,out long created,out long exited,out long kernel,out long user);
 }
}
