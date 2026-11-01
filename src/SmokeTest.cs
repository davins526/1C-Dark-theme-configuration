using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

namespace Dark1C {
    static class SmokeTest {
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern IntPtr LoadLibraryEx(string path,IntPtr reserved,uint flags);
        [DllImport("kernel32.dll",CharSet=CharSet.Ansi)]static extern IntPtr GetProcAddress(IntPtr module,string name);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate IntPtr Image(int format,int w,int h);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate IntPtr Create(IntPtr surface);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate void Rgb(IntPtr cr,double r,double g,double b);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate void Action1(IntPtr cr);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate void Rectangle(IntPtr cr,double x,double y,double w,double h);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate IntPtr Data(IntPtr surface);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate int Png(IntPtr surface,[MarshalAs(UnmanagedType.LPUTF8Str)]string path);
        static T Function<T>(IntPtr module,string name) where T:class {IntPtr address=GetProcAddress(module,name);if(address==IntPtr.Zero)throw new Exception("No export "+name);return Marshal.GetDelegateForFunctionPointer(address,typeof(T)) as T;}
        static string Paint(IntPtr cairo,string png) {
            IntPtr surface=Function<Image>(cairo,"cairo_image_surface_create")(0,900,600);
            IntPtr cr=Function<Create>(cairo,"cairo_create")(surface);
            try {
                var rgb=Function<Rgb>(cairo,"cairo_set_source_rgb");var fill=Function<Action1>(cairo,"cairo_fill");var rect=Function<Rectangle>(cairo,"cairo_rectangle");
                rgb(cr,1,1,1);rect(cr,0,0,900,600);fill(cr);
                rgb(cr,0,0,0);rect(cr,10,10,30,30);fill(cr);
                rgb(cr,.1,.7,.9);rect(cr,50,10,30,30);fill(cr);
                Function<Action1>(cairo,"cairo_surface_flush")(surface);
                IntPtr data=Function<Data>(cairo,"cairo_image_surface_get_data")(surface);
                int bg=Marshal.ReadInt32(data,300*900*4+450*4)&0xffffff;
                int text=Marshal.ReadInt32(data,20*900*4+20*4)&0xffffff;
                int accent=Marshal.ReadInt32(data,20*900*4+60*4)&0xffffff;
                Function<Png>(cairo,"cairo_surface_write_to_png")(surface,png);
                return bg.ToString("X6")+" "+text.ToString("X6")+" "+accent.ToString("X6");
            }finally{Function<Action1>(cairo,"cairo_destroy")(cr);Function<Action1>(cairo,"cairo_surface_destroy")(surface);}
        }
        static void Wait(Func<bool> ready,string message){for(int i=0;i<300;i++){if(ready())return;Thread.Sleep(100);}throw new Exception("Timeout: "+message);}
        static int Main(string[] args) {
            try {
                if(args.Length==3&&args[0]=="--host") {
                    string dir=args[1];IntPtr cairo=LoadLibraryEx(args[2],IntPtr.Zero,0x1100);if(cairo==IntPtr.Zero)throw new System.ComponentModel.Win32Exception();
                    File.WriteAllText(Path.Combine(dir,"baseline.txt"),Paint(cairo,Path.Combine(dir,"baseline.png")));
                    for(int stage=1;stage<=7;stage++) {
                        int current=stage;Wait(delegate{return File.Exists(Path.Combine(dir,"stage"+current));},"stage");
                        File.WriteAllText(Path.Combine(dir,"result"+stage+".txt"),Paint(cairo,Path.Combine(dir,"stage"+stage+".png")));
                    }
                    return 0;
                }
                string output=args[0],cairoPath=args[1];Directory.CreateDirectory(output);
                string exe=Assembly.GetExecutingAssembly().Location;
                using(Process host=Process.Start(new ProcessStartInfo(exe,"--host \""+output+"\" \""+cairoPath+"\""){UseShellExecute=false,CreateNoWindow=true})) {
                    try {
                        Wait(delegate{return File.Exists(Path.Combine(output,"baseline.txt"));},"baseline");
                        string baseline=File.ReadAllText(Path.Combine(output,"baseline.txt"));
                        typeof(Native).GetMethod("InjectCore",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{host});
                        Wait(delegate{int[] s=Native.State(host.Id,null,null,null);return s!=null&&s[1]!=0;},"native ready");
                        int[] state=Native.State(host.Id,null,null,null);if(state[1]!=1)throw new Exception("Native setup failed: "+state[4]);
                        Console.WriteLine("Hooks: "+state[3]);
                        for(int stage=1;stage<=7;stage++) {
                            bool on=stage!=3&&stage!=7;int theme=stage==4||stage==6?1:0;
                            Native.State(host.Id,on?1:0,stage==2||stage==6?1:0,35,theme);
                            Wait(delegate{var s=Native.State(host.Id,null,null,null);return s[2]==(on?1:0)&&s[10]==theme;},"switch");
                            File.WriteAllText(Path.Combine(output,"stage"+stage),"");
                            int current=stage;Wait(delegate{return File.Exists(Path.Combine(output,"result"+current+".txt"));},"paint");
                            string result=File.ReadAllText(Path.Combine(output,"result"+stage+".txt"));Console.WriteLine("Stage "+stage+": "+result);
                            if((stage==1||stage==5)&&!result.StartsWith("1E1E1E DADADA"))throw new Exception("Theme pixel mismatch");
                            if(stage==2&&(result.Split(' ')[0]=="1E1E1E"))throw new Exception("Wallpaper did not render");
                            if((stage==3||stage==7)&&result!=baseline)throw new Exception("OFF did not restore original drawing");
                            if(stage==4&&!result.StartsWith("FAFAFA 202020"))throw new Exception("Light palette mismatch");
                            if(stage==6&&result.Split(' ')[0]=="FAFAFA")throw new Exception("Light wallpaper did not render");
                            if(result.Split(' ')[2]!=baseline.Split(' ')[2])throw new Exception("Accent changed");
                        }
                        if(!host.WaitForExit(5000)||host.ExitCode!=0)throw new Exception("Host exit failed");
                        Console.WriteLine("PASS: injection, dark/light palettes, wallpaper in both themes, accents, repeated switching, OFF, clean exit.");
                    }finally{if(!host.HasExited)host.Kill();}
                }
                return 0;
            }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
        }
    }
}
