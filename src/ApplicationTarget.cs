using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace WindowsAppSender {
 public sealed class ApplicationTarget {
  public ulong WindowHandle {get;private set;}
  public int ProcessId {get;private set;}
  public long ProcessStartTicks {get;private set;}
  public string Executable {get;private set;}
  public string Title {get;private set;}
  public ApplicationTarget(ulong window,int pid,long start,string executable,string title) {
   WindowHandle=window;ProcessId=pid;ProcessStartTicks=start;Executable=executable;Title=title;
  }
  public override string ToString(){return Executable+" [PID "+ProcessId+"] — "+Title;}
  public bool SameInstance(ApplicationTarget other){return other!=null&&WindowHandle==other.WindowHandle&&ProcessId==other.ProcessId&&ProcessStartTicks==other.ProcessStartTicks;}
  public void Validate(string language) {
   uint pid;IntPtr hwnd=new IntPtr(unchecked((long)WindowHandle));
   if(WindowHandle==0||!IsWindow(hwnd)||!IsWindowVisible(hwnd))throw new InvalidOperationException(I18n.T(language,"The selected window is no longer available. Refresh the application list."));
   GetWindowThreadProcessId(hwnd,out pid);
   if(pid!=ProcessId)throw new InvalidOperationException(I18n.T(language,"The selected application changed. Refresh and select it again."));
   try {using(var process=new ProcessIdentity(ProcessId)) {
    if(!process.IsRunning||process.StartTicks!=ProcessStartTicks)throw new InvalidOperationException(I18n.T(language,"The selected application changed. Refresh and select it again."));
   }}catch(ArgumentException){throw new InvalidOperationException(I18n.T(language,"The selected application changed. Refresh and select it again."));}
   if(IsIconic(hwnd))throw new InvalidOperationException(I18n.T(language,"Restore the selected application window before starting."));
  }
  public static List<ApplicationTarget> List() {
   var result=new List<ApplicationTarget>();int own;
   using(var p=Process.GetCurrentProcess())own=p.Id;
   EnumWindows((hwnd,unused)=>{
    if(!IsWindowVisible(hwnd)||GetWindowTextLength(hwnd)==0)return true;
    uint pid;GetWindowThreadProcessId(hwnd,out pid);if(pid==0||pid==own)return true;
    int cloaked;if(DwmGetWindowAttribute(hwnd,14,out cloaked,4)==0&&cloaked!=0)return true;
    var title=new StringBuilder(GetWindowTextLength(hwnd)+1);GetWindowText(hwnd,title,title.Capacity);
    try {using(var p=Process.GetProcessById((int)pid))using(var identity=new ProcessIdentity((int)pid)) {
     if(identity.IsRunning)result.Add(new ApplicationTarget(unchecked((ulong)hwnd.ToInt64()),p.Id,identity.StartTicks,p.ProcessName+".exe",title.ToString()));
    }}catch(Exception e)when(e is ArgumentException||e is InvalidOperationException||e is System.ComponentModel.Win32Exception){}
    return true;
   },IntPtr.Zero);
   return result.OrderBy(t=>t.Executable,StringComparer.OrdinalIgnoreCase).ThenBy(t=>t.Title,StringComparer.OrdinalIgnoreCase).ToList();
  }
  delegate bool EnumProc(IntPtr hwnd,IntPtr data);
  [DllImport("user32.dll")]static extern bool EnumWindows(EnumProc proc,IntPtr data);
  [DllImport("user32.dll")]static extern bool IsWindow(IntPtr hwnd);
  [DllImport("user32.dll")]static extern bool IsWindowVisible(IntPtr hwnd);
  [DllImport("user32.dll")]static extern bool IsIconic(IntPtr hwnd);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetWindowTextLength(IntPtr hwnd);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetWindowText(IntPtr hwnd,StringBuilder value,int length);
  [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr hwnd,out uint pid);
  [DllImport("dwmapi.dll")]static extern int DwmGetWindowAttribute(IntPtr hwnd,int attribute,out int value,int length);
 }
}
