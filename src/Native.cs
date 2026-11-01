using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace Dark1C {
    internal static class Native {
        [DllImport("kernel32.dll",SetLastError=true)] static extern IntPtr OpenProcess(uint rights,bool inherit,int pid);
        [DllImport("kernel32.dll",SetLastError=true)] static extern IntPtr VirtualAllocEx(IntPtr process,IntPtr address,UIntPtr size,uint allocation,uint protect);
        [DllImport("kernel32.dll",SetLastError=true)] static extern bool VirtualFreeEx(IntPtr process,IntPtr address,UIntPtr size,uint type);
        [DllImport("kernel32.dll",SetLastError=true)] static extern bool WriteProcessMemory(IntPtr process,IntPtr address,byte[] data,UIntPtr count,out UIntPtr written);
        [DllImport("kernel32.dll",SetLastError=true)] static extern IntPtr CreateRemoteThread(IntPtr process,IntPtr attributes,UIntPtr stack,IntPtr start,IntPtr argument,uint flags,IntPtr tid);
        [DllImport("kernel32.dll")] static extern uint WaitForSingleObject(IntPtr handle,uint ms);
        [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern IntPtr GetModuleHandle(string name);
        [DllImport("kernel32.dll",CharSet=CharSet.Ansi)] static extern IntPtr GetProcAddress(IntPtr module,string name);
        [DllImport("kernel32.dll",EntryPoint="GetModuleHandleExW")] static extern bool GetModuleHandleEx(uint flags,IntPtr address,out IntPtr module);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern uint GetModuleFileName(IntPtr module,StringBuilder name,int size);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern IntPtr OpenFileMapping(uint access,bool inherit,string name);
        [DllImport("kernel32.dll",SetLastError=true)] static extern IntPtr MapViewOfFile(IntPtr mapping,uint access,uint hi,uint lo,UIntPtr bytes);
        [DllImport("kernel32.dll")] static extern bool UnmapViewOfFile(IntPtr address);
        [DllImport("kernel32.dll",SetLastError=true)] static extern bool IsWow64Process(IntPtr process,out bool wow);
        delegate bool WindowCallback(IntPtr hwnd,IntPtr arg);
        [DllImport("user32.dll")] static extern bool EnumWindows(WindowCallback callback,IntPtr arg);
        [DllImport("user32.dll")] static extern bool EnumChildWindows(IntPtr parent,WindowCallback callback,IntPtr arg);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd,out int pid);
        [DllImport("user32.dll")] static extern bool RedrawWindow(IntPtr hwnd,IntPtr rect,IntPtr region,uint flags);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr hwnd,StringBuilder value,int count);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr hwnd,StringBuilder value,int count);
        internal const string DllName="Dark1C.Native.dll";
        internal static string Root { get { return Path.GetDirectoryName(typeof(Native).Assembly.Location); } }
        internal static string ProcessPath(Process p) { return p.MainModule.FileName; }
        internal static void Validate(Process p) {
            string path=ProcessPath(p);
            if(!Path.GetFileName(path).Equals("1cv8.exe",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Подключение разрешено только к 1cv8.exe (Конфигуратор).");
            string v=FileVersionInfo.GetVersionInfo(path).FileVersion;
            if(v!="8.5.1.1529")throw new InvalidOperationException("Эта сборка рассчитана на 8.5.1.1529. Найдена: "+v);
            bool wow;
            if(!IsWow64Process(p.Handle,out wow)||wow)throw new InvalidOperationException("Нужен процесс x64.");
            using(var query=new System.Management.ManagementObjectSearcher("SELECT CommandLine FROM Win32_Process WHERE ProcessId="+p.Id)) {
                bool designer=false;
                foreach(System.Management.ManagementObject item in query.Get()) {
                    string command=Convert.ToString(item["CommandLine"]);
                    designer=System.Text.RegularExpressions.Regex.IsMatch(command,@"(?:^|\s)DESIGNER(?:\s|$)",System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                }
                if(!designer)throw new InvalidOperationException("Не удалось подтвердить режим DESIGNER. Открой Конфигуратор отдельно и обнови список.");
            }
        }
        internal static int[] State(int pid,int? request,int? wallpaper,int? darkness,int? theme=null) {
            if(darkness.HasValue&&(darkness.Value<0||darkness.Value>100))throw new ArgumentOutOfRangeException("darkness");
            if(theme.HasValue&&(theme.Value<0||theme.Value>1))throw new ArgumentOutOfRangeException("theme");
            IntPtr mapping=OpenFileMapping(6,false,"Local\\Dark1C.State.v2."+pid);
            if(mapping==IntPtr.Zero)return null;
            try {
                IntPtr view=MapViewOfFile(mapping,6,0,0,(UIntPtr)44);
                if(view==IntPtr.Zero)throw new Win32Exception();
                try {
                    if(Marshal.ReadInt32(view,0)!=0x44314331||Marshal.ReadInt32(view,32)!=2)throw new InvalidOperationException("Несовместимый протокол Dark1C. Перезапусти тестовый Конфигуратор после обновления DLL.");
                    if(wallpaper.HasValue)Marshal.WriteInt32(view,20,wallpaper.Value);
                    if(darkness.HasValue)Marshal.WriteInt32(view,24,darkness.Value);
                    if(theme.HasValue)Marshal.WriteInt32(view,36,theme.Value);
                    if(request.HasValue)Marshal.WriteInt32(view,28,request.Value);
                    int[] s=new int[11];Marshal.Copy(view,s,0,11);return s;
                }finally {UnmapViewOfFile(view);}
            }finally{CloseHandle(mapping);}
        }
        internal static void Inject(Process p) {
            Validate(p);
            InjectCore(p);
        }
        // Kept private so the test driver can exercise the exact loader against
        // an owned disposable host; the UI always enters through Validate().
        static void InjectCore(Process p) {
            string dll=Path.Combine(Root,DllName);
            if(!File.Exists(dll))throw new FileNotFoundException("Нет нативного модуля",dll);
            foreach(ProcessModule m in p.Modules) {
                if(m.ModuleName.Equals(DllName,StringComparison.OrdinalIgnoreCase)) {
                    if(!Path.GetFullPath(m.FileName).Equals(Path.GetFullPath(dll),StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("В процессе загружена другая копия Dark1C. Перезапусти этот Конфигуратор.");
                    return;
                }
            }
            IntPtr local=GetProcAddress(GetModuleHandle("kernel32.dll"),"LoadLibraryW"),owner;
            if(local==IntPtr.Zero||!GetModuleHandleEx(6,local,out owner))throw new Win32Exception();
            var ownerPath=new StringBuilder(1024);GetModuleFileName(owner,ownerPath,ownerPath.Capacity);
            string ownerName=Path.GetFileName(ownerPath.ToString());IntPtr remoteOwner=IntPtr.Zero;
            foreach(ProcessModule m in p.Modules)if(m.ModuleName.Equals(ownerName,StringComparison.OrdinalIgnoreCase))remoteOwner=m.BaseAddress;
            if(remoteOwner==IntPtr.Zero)throw new InvalidOperationException("Не найден системный модуль загрузчика.");
            IntPtr entry=new IntPtr(remoteOwner.ToInt64()+local.ToInt64()-owner.ToInt64());
            IntPtr process=OpenProcess(0x43a,false,p.Id);
            if(process==IntPtr.Zero)throw new Win32Exception(Marshal.GetLastWin32Error(),"Нет доступа к процессу. Проверь, что 1С и Dark1C запущены с одинаковыми правами.");
            IntPtr remote=IntPtr.Zero,thread=IntPtr.Zero;bool finished=true;
            try {
                byte[] bytes=Encoding.Unicode.GetBytes(dll+"\0");
                remote=VirtualAllocEx(process,IntPtr.Zero,(UIntPtr)bytes.Length,0x3000,4);if(remote==IntPtr.Zero)throw new Win32Exception();
                UIntPtr written;if(!WriteProcessMemory(process,remote,bytes,(UIntPtr)bytes.Length,out written)||written.ToUInt64()!=(ulong)bytes.Length)throw new Win32Exception();
                thread=CreateRemoteThread(process,IntPtr.Zero,UIntPtr.Zero,entry,remote,0,IntPtr.Zero);if(thread==IntPtr.Zero)throw new Win32Exception();
                finished=WaitForSingleObject(thread,15000)==0;
                if(!finished)throw new TimeoutException("Загрузчик DLL не завершился за 15 секунд. Буфер оставлен до завершения процесса. Не повторяй подключение; перезапусти тестовый Конфигуратор.");
            }finally{
                if(thread!=IntPtr.Zero)CloseHandle(thread);
                if(remote!=IntPtr.Zero&&finished)VirtualFreeEx(process,remote,UIntPtr.Zero,0x8000);
                CloseHandle(process);
            }
        }
        internal static void Apply(Process p,bool on,bool wallpaper,int darkness,int theme=0) {
            if(on)Inject(p);
            if(!on&&State(p.Id,null,null,null)==null)return;
            int[] state=null;
            for(int i=0;i<160;i++) {
                state=State(p.Id,null,null,null);
                if(state!=null&&state[1]!=0)break;
                Thread.Sleep(100);
            }
            if(state==null)throw new InvalidOperationException("DLL не ответила. Возможно, её заблокировала защита или отсутствует зависимость.");
            if(state[1]!=1)throw new InvalidOperationException("Перехваты не установлены. Код ошибки: "+state[4]);
            State(p.Id,on?1:0,wallpaper?1:0,darkness,theme);
            for(int i=0;i<30;i++) {Thread.Sleep(100);state=State(p.Id,null,null,null);if(state==null)throw new InvalidOperationException("Процесс завершился во время применения темы.");if(state[2]==(on?1:0)&&state[10]==theme){Redraw(p.Id);return;}}
            throw new TimeoutException("DLL не подтвердила переключение темы.");
        }
        internal static void Redraw(int pid) {
            EnumWindows(delegate(IntPtr h,IntPtr a){int owner;GetWindowThreadProcessId(h,out owner);if(owner==pid)RedrawWindow(h,IntPtr.Zero,IntPtr.Zero,0x85);return true;},IntPtr.Zero);
        }
        internal static string Windows(int pid) {
            var result=new StringBuilder();
            WindowCallback record=delegate(IntPtr h,IntPtr a){var cls=new StringBuilder(256);GetClassName(h,cls,256);result.AppendLine(cls.ToString());return true;};
            EnumWindows(delegate(IntPtr h,IntPtr a){int owner;GetWindowThreadProcessId(h,out owner);if(owner==pid){record(h,a);EnumChildWindows(h,record,IntPtr.Zero);}return true;},IntPtr.Zero);
            return result.ToString(); // Class names only; no window titles or module text.
        }
    }
}
