using System;
using System.Diagnostics;
namespace Dark1C {
    static class ControlCommand {
        static int Main(string[] args) {
            try {
                if(args.Length<2){Console.WriteLine("Dark1C.Control.exe on|off|state|classes PID [wallpaper:0|1] [darkness:0..100]");return 2;}
                int pid=int.Parse(args[1]);using(Process p=Process.GetProcessById(pid)) {
                    Native.Validate(p);
                    if(args[0]=="on"||args[0]=="off")Native.Apply(p,args[0]=="on",args.Length>2&&args[2]=="1",args.Length>3?int.Parse(args[3]):65);
                    if(args[0]=="classes")Console.Write(Native.Windows(pid));
                    else {var s=Native.State(pid,null,null,null);Console.WriteLine(s==null?"Not loaded":"Ready="+s[1]+" Enabled="+s[2]+" Hooks="+s[3]+" Error="+s[4]);}
                }return 0;
            }catch(Exception ex){Console.Error.WriteLine(ex.Message);return 1;}
        }
    }
}
